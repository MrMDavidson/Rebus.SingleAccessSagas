namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Describes information about the concurrency for a message
	/// </summary>
	public class ConcurrencyControlInfo {
		/// <summary>
		/// Identifier of the lock. This controls the scope of the lock.
		/// </summary>
		/// <example>You might set this to be message.GetType().FillName to provide a system wide throttle for a given message type</example>
		/// <example>You might set this to be message.CustomerId.ToString() to throttle the number of messages a given customer can process at once</example>
		/// <example>You might set this to be $"{message.GetType().FullName}:{message.CustomerId}" to throttle the number of messages, of a specific type, the system will process</example>
		public object LockIdentifier { get; }

		/// <summary>
		/// The maximum number of messages, for <seealso cref="LockIdentifier"/>, that can be processed at once.
		/// </summary>
		/// <remarks>Ensure that for a given <seealso cref="LockIdentifier"/> you always set the same <see cref="MaxConcurrency"/>. Results will be unpredictable otherwise.</remarks>
		public int MaxConcurrency {get; }

		/// <summary>
		/// Constructs a new <see cref="ConcurrencyControlInfo"/>
		/// </summary>
		/// <param name="identifier">Identifier of the lock</param>
		/// <param name="maxConcurrency">Maximum number of concurrent handlers</param>
		public ConcurrencyControlInfo(object identifier, int maxConcurrency = 1) {
			LockIdentifier = identifier;
			MaxConcurrency = maxConcurrency;
		}
	}
}