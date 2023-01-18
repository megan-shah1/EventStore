namespace EventStore.Core.Data {
	public enum VNodeStartupState {
		Chasing = 0,
		ChaserCaughtUp = 1,
		WaitingForConditions = 2,
		Complete = 3,
	}
}
