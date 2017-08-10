namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Marker interface used to identify a <seealso cref="Rebus.Sagas.Saga{TSagaData}"/> implementation as requiring single handling across all workers
	/// </summary>
	public interface ISingleAccessSaga {
	}
}