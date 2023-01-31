using System;
using System.Diagnostics.Metrics;
using EventStore.Core.Telemetry;

namespace EventStore.Core;

public class GossipStatsCollector {
	private readonly DurationTracker _tracker;

	internal GossipStatsCollector(DurationMetric metric, string name) {
		_tracker = new DurationTracker(metric, name);
	}

	public Duration StartRequest() {
		return _tracker.Start();
	}
}
