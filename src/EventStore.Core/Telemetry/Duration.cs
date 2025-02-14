﻿using System;
using System.Collections.Generic;

namespace EventStore.Core.Telemetry {
	public struct Duration : IDisposable {
		private readonly DurationMetric _metric;
		private readonly string _name;
		private readonly Instant _start;
		private bool _failed;

		public Duration(DurationMetric metric, string name, Instant start) {
			_metric = metric;
			_name = name;
			_start = start;
			_failed = false;
		}

		public void SetException(Exception ex) {
			_failed = true;
		}

		public void Dispose() {
			_metric?.Record(
				_start,
				new KeyValuePair<string, object>("activity", _name),
				new KeyValuePair<string, object>("status", _failed ? "failed" : "successful"));
		}
	}
}
