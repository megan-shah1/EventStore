﻿using System;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Exceptions;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using Serilog;

namespace EventStore.Core.Services {
	public class RedactionService :
		IHandle<RedactionMessage.SwitchChunkLock>,
		IHandle<RedactionMessage.SwitchChunk>,
		IHandle<RedactionMessage.SwitchChunkUnlock> {

		private readonly TFChunkDb _db;
		private readonly SemaphoreSlim _switchChunksSemaphore;
		private const string NewChunkFileExtension = ".tmp";

		public RedactionService(TFChunkDb db, SemaphoreSlim switchChunksSemaphore) {
			Ensure.NotNull(db, nameof(db));
			Ensure.NotNull(switchChunksSemaphore, nameof(switchChunksSemaphore));

			_db = db;
			_switchChunksSemaphore = switchChunksSemaphore;

			Thread.MemoryBarrier();
		}

		public void Handle(RedactionMessage.SwitchChunkLock message) {
			Thread.MemoryBarrier();

			if (_switchChunksSemaphore.Wait(TimeSpan.Zero))
				message.Envelope.ReplyWith(new RedactionMessage.SwitchChunkLockSucceeded());
			else
				message.Envelope.ReplyWith(new RedactionMessage.SwitchChunkLockFailed());
		}

		public void Handle(RedactionMessage.SwitchChunkUnlock message) {
			Thread.MemoryBarrier();

			try {
				_switchChunksSemaphore.Release();
				message.Envelope.ReplyWith(new RedactionMessage.SwitchChunkUnlockSucceeded());
			} catch (SemaphoreFullException) {
				message.Envelope.ReplyWith(new RedactionMessage.SwitchChunkUnlockFailed());
				throw;
			}
		}

		public void Handle(RedactionMessage.SwitchChunk message) {
			ThreadPool.QueueUserWorkItem(_ => {
				try {
					Thread.MemoryBarrier();
					SwitchChunk(message.TargetChunkFile, message.NewChunkFile, message.Envelope);
				} catch (Exception ex) {
					Log.Error(ex, "An error has occurred when trying to switch chunk: {targetChunk} with chunk: {newChunk}.",
						message.TargetChunkFile, message.NewChunkFile);
					message.Envelope.ReplyWith(new RedactionMessage.SwitchChunkFailed("An unexpected error has occurred."));
				}
			});
		}

		private void SwitchChunk(string targetChunkFile, string newChunkFile, IEnvelope envelope) {
			if (!IsValidSwitchChunkRequest(targetChunkFile, newChunkFile, out var newChunk, out var failReason)) {
				envelope.ReplyWith(new RedactionMessage.SwitchChunkFailed(failReason));
				return;
			}

			_db.Manager.SwitchChunk(
				chunk: newChunk,
				verifyHash: false,
				removeChunksWithGreaterNumbers: false);

			envelope.ReplyWith(new RedactionMessage.SwitchChunkSucceeded());
		}

		private static bool IsUnsafeFileName(string fileName) {
			// protect against directory traversal attacks
			return fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..");
		}

		private bool IsValidSwitchChunkRequest(string targetChunkFile, string newChunkFile, out TFChunk newChunk, out string failReason) {
			newChunk = null;

			if (IsUnsafeFileName(targetChunkFile) || IsUnsafeFileName(newChunkFile)) {
				failReason = "Invalid file name.";
				return false;
			}

			int targetChunkNumber;
			try {
				targetChunkNumber = _db.Config.FileNamingStrategy.GetIndexFor(targetChunkFile);
			} catch {
				failReason = "The target chunk's file name is not valid.";
				return false;
			}

			if (Path.GetExtension(newChunkFile) != NewChunkFileExtension) {
				failReason = $"The new chunk's file extension is not: {NewChunkFileExtension}";
				return false;
			}

			if (!File.Exists(Path.Combine(_db.Config.Path, targetChunkFile))) {
				failReason = "The target chunk file does not exist in the database directory.";
				return false;
			}

			var newChunkPath = Path.Combine(_db.Config.Path, newChunkFile);
			if (!File.Exists(newChunkPath)) {
				failReason = "The new chunk file does not exist in the database directory.";
				return false;
			}

			TFChunk targetChunk;
			try {
				targetChunk = _db.Manager.GetChunk(targetChunkNumber);
			} catch {
				failReason = $"Failed to retrieve the chunk with number: {targetChunkNumber}.";
				return false;
			}

			if (Path.GetFileName(targetChunk.FileName) != targetChunkFile) {
				failReason = "The target chunk file is no longer actively used by the database.";
				return false;
			}

			if (targetChunk.ChunkFooter is not { IsCompleted: true }) {
				failReason = "The target chunk is not a completed chunk.";
				return false;
			}

			ChunkHeader newChunkHeader;
			ChunkFooter newChunkFooter;
			try {
				using var fs = new FileStream(newChunkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				newChunkHeader = ChunkHeader.FromStream(fs);
				fs.Seek(-ChunkFooter.Size, SeekOrigin.End);
				newChunkFooter = ChunkFooter.FromStream(fs);
			} catch (Exception ex) {
				failReason = $"Failed to read the new chunk's header or footer: {ex.Message}";
				return false;
			}

			if (newChunkHeader.ChunkStartNumber != targetChunk.ChunkHeader.ChunkStartNumber ||
			    newChunkHeader.ChunkEndNumber != targetChunk.ChunkHeader.ChunkEndNumber) {
				failReason = $"The target chunk's range: {targetChunk.ChunkHeader.ChunkStartNumber} - {targetChunk.ChunkHeader.ChunkEndNumber} does not match "
				         + $"the new chunk's range: {newChunkHeader.ChunkStartNumber} - {newChunkHeader.ChunkEndNumber}.";
				return false;
			}

			if (!newChunkFooter.IsCompleted) {
				failReason = "The new chunk is not a completed chunk.";
				return false;
			}

			try {
				newChunk = TFChunk.FromCompletedFile(
					filename: newChunkPath,
					verifyHash: true,
					unbufferedRead: true,
					initialReaderCount: 1,
					maxReaderCount: 1,
					optimizeReadSideCache: false,
					reduceFileCachePressure: true);
			} catch (HashValidationException) {
				failReason = "The new chunk has failed hash verification.";
				return false;
			} catch (Exception ex) {
				failReason = $"Failed to open the new chunk: {ex.Message}";
				return false;
			} finally {
				newChunk?.Dispose();
			}

			failReason = null;
			return true;
		}
	}
}
