﻿using System;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ReplicationTrackingMessage {
		[DerivedMessage]
		public partial class WriterCheckpointFlushed : Message {
		}

		[DerivedMessage]
		public partial class IndexedTo : Message {
			public readonly long LogPosition;
			
			public IndexedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[DerivedMessage]
		public partial class ReplicatedTo : Message {
			public readonly long LogPosition;
			
			public ReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[DerivedMessage]
		public partial class LeaderReplicatedTo : Message {
			public readonly long LogPosition;
			
			public LeaderReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition + 1, "logPosition");
				LogPosition = logPosition;
			}
		}

		[DerivedMessage]
		public partial class ReplicaWriteAck : Message {
			public readonly Guid SubscriptionId;
			public readonly long ReplicationLogPosition;

			public ReplicaWriteAck(Guid subscriptionId, long replicationLogPosition) {
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				SubscriptionId = subscriptionId;
				ReplicationLogPosition = replicationLogPosition;
			}
		}
	}
}
