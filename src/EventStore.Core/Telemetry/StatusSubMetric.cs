﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace EventStore.Core.Telemetry {
	// Use this class if the status can transition from one to another, as in a state machine
	// Use ActivityStatusMetric if the status represents an activity that starts and stops
	//
	// This does create a time series for each name * status that gets instantiated.
	// However, prometheus does efficiently store series whose values are not changing.
	//
	// The component name and status are stored in tags
	// The value contains time in seconds since epoch so we can tell which status is active
	public class StatusSubMetric {
		private readonly KeyValuePair<string, object>[] _tags;
		private string _status;

		public StatusSubMetric(string componentName, object initialStatus, StatusMetric metric) {
			_status = initialStatus?.ToString();
			_tags = new[] {
				new KeyValuePair<string, object>("name", componentName),
				new KeyValuePair<string, object>("status", _status),
			};

			metric.Add(this);
		}

		public void SetStatus(string status) {
			Interlocked.Exchange(ref _status, status);
		}

		public Measurement<long> Observe(long secondsSinceEpoch) {
			_tags[1] = new KeyValuePair<string, object>("status", _status);
			return new Measurement<long>(secondsSinceEpoch, _tags.AsSpan());
		}
	}
}
