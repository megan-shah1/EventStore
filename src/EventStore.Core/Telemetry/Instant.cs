﻿using System;
using System.Diagnostics;

namespace EventStore.Core.Telemetry;

// this provides stronger typing than just passing a long representing the number of ticks
// and provides us a place to change the resolution and size if long ticks is overkill.
public struct Instant {
	private static readonly double _secondsPerTick = 1 / (double)Stopwatch.Frequency;

	public static Instant Now => new(Stopwatch.GetTimestamp());
	public static Instant FromSeconds(long seconds) => new(stopwatchTicks: seconds * Stopwatch.Frequency);

	public static bool operator ==(Instant x, Instant y) => x._ticks == y._ticks;
	public static bool operator !=(Instant x, Instant y) => x._ticks != y._ticks;

	private static double TicksToSeconds(long ticks) => ticks * _secondsPerTick;

	private readonly long _ticks;

	// Stopwatch Ticks, not DateTime Ticks - these can be different.
	private Instant(long stopwatchTicks) {
		_ticks = stopwatchTicks;
	}

	public double ElapsedSecondsSince(Instant start) => TicksToSeconds(ElapsedTicksSince(start));

	// something has gone wrong if we call this
	public override bool Equals(object obj) =>
		throw new InvalidOperationException();

	public override int GetHashCode() =>
		_ticks.GetHashCode();

	private long ElapsedTicksSince(Instant since) => _ticks - since._ticks;
}
