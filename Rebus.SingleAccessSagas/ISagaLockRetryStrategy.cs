using System;
using Rebus.Messages;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Responsible for determining the retry interval for messages which have failed to acquire all saga locks
	/// </summary>
	public interface ISagaLockRetryStrategy {
		/// <summary>
		/// Determine the delay before attempting to retry processing of a message
		/// </summary>
		/// <param name="failedLock">The last <seealso cref="ISagaLock"/> which could not be acquired. Note: Caller will dispose of the lock</param>
		/// <param name="message">Message being processed</param>
		/// <returns>Delay before retrying processing of the current message</returns>
		TimeSpan GetMessageRetryInterval(ISagaLock failedLock, Message message);
	}
}