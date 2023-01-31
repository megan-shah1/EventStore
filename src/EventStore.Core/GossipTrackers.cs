using System.Diagnostics.Metrics;
using EventStore.Core.Telemetry;

namespace EventStore.Core;

public class GossipTrackers {
	public static readonly GossipTrackers Default = new();
	private DurationMetric _metric;

	private GossipTrackers()
	{
	}

	public Meter CoreMeter {
		set {
			_metric = new DurationMetric(value, "gossip");
		}
	}

	public GossipStatsCollector Create(string name) {
		return new GossipStatsCollector(_metric, name);
	}
}
