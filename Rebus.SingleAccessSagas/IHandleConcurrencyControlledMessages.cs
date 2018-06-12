using Rebus.Handlers;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Marker interface to indicate that a handler should have concurrency controls applied to it
	/// </summary>
	public interface IHandleConcurrencyControlledMessages : IHandleMessages {

	}

	/// <summary>
	/// Indicates that a handler has concurrency controls applied to handling of the messages
	/// </summary>
	/// <typeparam name="TMessageType">Type of message being received</typeparam>
	public interface IHandleConcurrencyControlledMessages<in TMessageType> : IHandleConcurrencyControlledMessages, IHandleMessages<TMessageType> {
		/// <summary>
		/// Get concurrency controls to be applied to <paramref name="message"/>
		/// </summary>
		/// <param name="message">Message having concurrency controls applied</param>
		/// <returns><c>null</c> if there's no concurrency controls for <paramref name="message"/> otherwise a <seealso cref="ConcurrencyControlInfo"/> describing the concurrency to be applied</returns>
		ConcurrencyControlInfo GetConcurrencyControlInfoForMessage(TMessageType message);
	}
}