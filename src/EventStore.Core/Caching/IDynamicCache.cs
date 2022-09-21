﻿namespace EventStore.Core.Caching {
	// This has its capacity adjusted by the ICacheResizer
	public interface IDynamicCache {
		string Name { get; }
		long Capacity { get; }
		long Size { get; }
		// Approximate amount freed but not yet garbage collected
		long FreedSize { get; }
		void SetCapacity(long capacity);
		void ResetFreedSize();
		// todo: hits and misses
	}
}
