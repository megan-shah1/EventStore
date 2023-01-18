using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.VNode;
using EventStore.Core.Telemetry;
using EventStore.Core.XUnit.Tests.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Startup {
	public class StartupStatusTrackerTests {
		private readonly FakeClock _clock = new();
		private readonly StatusMetric _metric;
		private readonly StartupStatusTracker _sut;

		public StartupStatusTrackerTests() {
			_metric = new StatusMetric(
				new Meter($"Eventstore.Core.XUnit.Tests.{nameof(StartupStatusTrackerTests)}"),
				"eventstore-statuses",
				_clock);
			_sut = new StartupStatusTracker(_metric);
		}

		[Fact]
		public void can_observe_caughtup() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Chasing", 500, _metric.Observe());

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(VNodeStartupState.ChaserCaughtUp);
			AssertMeasurements("ChaserCaughtUp", 502, _metric.Observe());
		}

		[Fact]
		public void can_observe_waiting_for_conditions() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Chasing", 500, _metric.Observe());

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(VNodeStartupState.WaitingForConditions);
			AssertMeasurements("WaitingForConditions", 502, _metric.Observe());
		}

		[Fact]
		public void can_observe_complete() {
			_clock.SecondsSinceEpoch = 500;
			AssertMeasurements("Chasing", 500, _metric.Observe());

			_clock.SecondsSinceEpoch = 502;
			_sut.OnStateChange(VNodeStartupState.Complete);
			AssertMeasurements("Complete", 502, _metric.Observe());
		}

		static void AssertMeasurements(
			string expectedStatus,
			int expectedValue,
			IEnumerable<Measurement<long>> measurements) {

			Assert.Collection(measurements.ToArray(),
				m => {
					Assert.Equal(expectedValue, m.Value);
					Assert.Collection(
						m.Tags.ToArray(),
						t => {
							Assert.Equal("name", t.Key);
							Assert.Equal("Startup", t.Value);
						},
						t => {
							Assert.Equal("status", t.Key);
							Assert.Equal(expectedStatus, t.Value);
						});
				});
		}
	}
}
