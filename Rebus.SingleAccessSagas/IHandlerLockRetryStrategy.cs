using System;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Responsible for determining the retry interval for messages which have failed to acquire all saga locks
	/// </summary>
	public interface IHandlerLockRetryStrategy {
		/// <summary>
		/// Determine the delay before attempting to retry processing of a message
		/// </summary>
		/// <param name="unlockedHandler">The <seealso cref="HandlerInvoker"/> for which the a lock could not be acquired.</param>
		/// <param name="message">Message being processed</param>
		/// <param name="context">Context for message processing</param>
		/// <returns>Delay before retrying processing of the current message</returns>
		TimeSpan GetMessageRetryInterval(HandlerInvoker unlockedHandler, Message message, IncomingStepContext context);
	}
}