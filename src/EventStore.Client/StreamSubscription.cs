using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client {
	public class StreamSubscription : IDisposable {
		private readonly IAsyncEnumerable<ResolvedEvent> _events;
		private readonly Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> _eventAppeared;
		private readonly Func<StreamSubscription, Position, CancellationToken, Task> _checkpointReached;
		private readonly Action<StreamSubscription, SubscriptionDroppedReason, Exception> _subscriptionDropped;
		private readonly CancellationToken _cancellationToken;
		private readonly CancellationTokenSource _disposed;
		private int _subscriptionDroppedInvoked;

		internal static async Task<StreamSubscription> Confirm(
			IAsyncEnumerable<(SubscriptionConfirmation, Position?, ResolvedEvent)> read,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Func<StreamSubscription, Position, CancellationToken, Task> checkpointReached = default,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			CancellationToken cancellationToken = default) {
			var enumerator = read.GetAsyncEnumerator(cancellationToken);
			if (!await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false) ||
			    enumerator.Current.Item1 == SubscriptionConfirmation.None) {
				throw new InvalidOperationException();
			}

			return new StreamSubscription(enumerator, eventAppeared, checkpointReached, subscriptionDropped,
				cancellationToken);
		}

		private StreamSubscription(IAsyncEnumerator<(SubscriptionConfirmation, Position?, ResolvedEvent)> events,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Func<StreamSubscription, Position, CancellationToken, Task> checkpointReached = default,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			CancellationToken cancellationToken = default) {
			if (events == null) {
				throw new ArgumentNullException(nameof(events));
			}

			if (eventAppeared == null) {
				throw new ArgumentNullException(nameof(eventAppeared));
			}

			_disposed = new CancellationTokenSource();
			_events = new Enumerable(events, CheckpointReached);
			_eventAppeared = eventAppeared;
			_checkpointReached = checkpointReached ?? ((_, __, ct) => Task.CompletedTask);
			_subscriptionDropped = subscriptionDropped;
			_cancellationToken = cancellationToken;
			_subscriptionDroppedInvoked = 0;

			Task.Run(Subscribe);
		}

		private Task CheckpointReached(Position position) => _checkpointReached(this, position, _cancellationToken);

		private async Task Subscribe() {
			try {
				await foreach (var resolvedEvent in _events.ConfigureAwait(false)) {
					try {
						if (_disposed.IsCancellationRequested) {
							SubscriptionDropped(SubscriptionDroppedReason.Disposed);
							return;
						}

						await _eventAppeared(this, resolvedEvent, _disposed.Token).ConfigureAwait(false);
					} catch (Exception ex) when (ex is ObjectDisposedException || ex is OperationCanceledException) {
						SubscriptionDropped(SubscriptionDroppedReason.Disposed);
						return;
					} catch (Exception ex) {
						try {
							SubscriptionDropped(SubscriptionDroppedReason.SubscriberError, ex);
						} finally {
							_disposed.Cancel();
						}

						return;
					}
				}
			} catch (Exception ex) {
				try {
					SubscriptionDropped(SubscriptionDroppedReason.ServerError, ex);
				} finally {
					_disposed.Cancel();
				}
			}
		}

		public void Dispose() {
			if (_disposed.IsCancellationRequested) {
				return;
			}

			SubscriptionDropped(SubscriptionDroppedReason.Disposed);

			_disposed.Dispose();
		}

		private void SubscriptionDropped(SubscriptionDroppedReason reason, Exception ex = null) {
			if (Interlocked.CompareExchange(ref _subscriptionDroppedInvoked, 1, 0) == 1) {
				return;
			}

			_subscriptionDropped?.Invoke(this, reason, ex);
		}

		private class Enumerable : IAsyncEnumerable<ResolvedEvent> {
			private readonly IAsyncEnumerator<(SubscriptionConfirmation, Position?, ResolvedEvent)> _inner;
			private readonly Func<Position, Task> _checkpointReached;

			public Enumerable(IAsyncEnumerator<(SubscriptionConfirmation, Position?, ResolvedEvent)> inner,
				Func<Position, Task> checkpointReached) {
				if (inner == null) {
					throw new ArgumentNullException(nameof(inner));
				}

				if (checkpointReached == null) {
					throw new ArgumentNullException(nameof(checkpointReached));
				}

				_inner = inner;
				_checkpointReached = checkpointReached;
			}

			public IAsyncEnumerator<ResolvedEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
				=> new Enumerator(_inner, _checkpointReached);

			private class Enumerator : IAsyncEnumerator<ResolvedEvent> {
				private readonly IAsyncEnumerator<(SubscriptionConfirmation, Position? position, ResolvedEvent
					resolvedEvent)> _inner;

				private readonly Func<Position, Task> _checkpointReached;

				public Enumerator(IAsyncEnumerator<(SubscriptionConfirmation, Position?, ResolvedEvent)> inner,
					Func<Position, Task> checkpointReached) {
					if (inner == null) {
						throw new ArgumentNullException(nameof(inner));
					}

					if (checkpointReached == null) {
						throw new ArgumentNullException(nameof(checkpointReached));
					}

					_inner = inner;
					_checkpointReached = checkpointReached;
				}

				public ValueTask DisposeAsync() => _inner.DisposeAsync();

				public async ValueTask<bool> MoveNextAsync() {
					ReadLoop:
					if (!await _inner.MoveNextAsync().ConfigureAwait(false)) {
						return false;
					}

					if (!_inner.Current.position.HasValue) {
						return true;
					}

					await _checkpointReached(_inner.Current.position.Value).ConfigureAwait(false);
					goto ReadLoop;
				}

				public ResolvedEvent Current => _inner.Current.resolvedEvent;
			}
		}
	}
}
