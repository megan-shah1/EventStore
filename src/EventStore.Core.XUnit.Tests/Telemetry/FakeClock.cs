﻿using System;
using EventStore.Core.Telemetry;

namespace EventStore.Core.XUnit.Tests.Telemetry {
	internal class FakeClock : IClock {
		public Instant Now => Instant.FromSeconds(SecondsSinceEpoch);
		public long SecondsSinceEpoch { get; set; }
	}
}
