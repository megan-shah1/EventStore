using EventStore.Core.Data;
using EventStore.Core.Telemetry;

namespace EventStore.Core.Services.VNode {
	public interface IStartupStatusTracker {
		void OnStateChange(VNodeStartupState newState);
	}

	public class StartupStatusTracker : IStartupStatusTracker {
		private readonly StatusSubMetric _subMetric;

		public StartupStatusTracker(StatusMetric metric) {
			_subMetric = new StatusSubMetric("Startup", VNodeStartupState.Chasing, metric);
		}

		public void OnStateChange(VNodeStartupState newState) =>
			_subMetric.SetStatus(newState.ToString());

		public class NoOp : IStartupStatusTracker {
			public void OnStateChange(VNodeStartupState newState) {
			}
		}
	}
}
