﻿using EventStore.Core.Caching;

namespace EventStore.Core.Tests.Caching {
	public class EmptyDynamicCache : IDynamicCache {
		public static EmptyDynamicCache Instance { get; } = new();
		public string Name => nameof(EmptyDynamicCache);
		public long Capacity { get; private set; }
		public void SetCapacity(long capacity) {
			Capacity = capacity;
		}
		public long Size => 0;
	}
}
