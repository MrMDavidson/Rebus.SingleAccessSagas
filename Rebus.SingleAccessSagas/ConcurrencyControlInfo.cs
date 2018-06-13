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
		public int MaxConcurrency { get; }

		/// <summary>
		/// The cost of the operation. You might have a scenario where several operations use the same lock identifier but are more intensive.
		///  </summary>
		/// <example>An operation that does tensor flow analysis might have a cost of 3. Whilst an operation that performs a CRUD DB operation might have a cost of 1</example>
		/// <remarks>If <seealso cref="MaxConcurrency"/> is equal to <seealso cref="OperationCost"/> only one operation will happen at once. If <seealso cref="MaxConcurrency"/> is greater than multiple operations can occur at once. If <seealso cref="OperationCost"/> is greater than no operations will ever be perfomed</remarks>
		public int OperationCost { get; }

		/// <summary>
		/// Constructs a new <see cref="ConcurrencyControlInfo"/>
		/// </summary>
		/// <param name="identifier">Identifier of the lock</param>
		/// <param name="maxConcurrency">Maximum number of concurrent handlers</param>
		/// <param name="operationCost">Cost of performing this operation.</param>
		public ConcurrencyControlInfo(object identifier, int maxConcurrency = 1, int operationCost = 1) {
			LockIdentifier = identifier;
			MaxConcurrency = maxConcurrency;
			OperationCost = operationCost;
		}
	}
}