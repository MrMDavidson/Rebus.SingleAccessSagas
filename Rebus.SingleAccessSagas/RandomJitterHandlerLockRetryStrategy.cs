using System;
using Rebus.Messages;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// An implementation of <seealso cref="IHandlerLockRetryStrategy"/> which uses a a random delay time between a specified maximum and minimum
	/// </summary>
	public class RandomJitterHandlerLockRetryStrategy : IHandlerLockRetryStrategy {
		private readonly Random _random;
		private readonly int _minimumDelayMs;
		private readonly int _maximumDelayMs;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="minimumDelay">Minimum amount of time to delay a message</param>
		/// <param name="maximumDelay">Maximum amountof time to delay a message</param>
		public RandomJitterHandlerLockRetryStrategy(TimeSpan minimumDelay, TimeSpan maximumDelay) {
			_random = new Random();

			if (minimumDelay < TimeSpan.Zero) {
				throw new ArgumentException($"{nameof(minimumDelay)} must be greater than zero", nameof(minimumDelay));
			}
			if (maximumDelay < TimeSpan.MinValue) {
				throw new ArgumentException($"{nameof(maximumDelay)} must be greater than zero", nameof(maximumDelay));
			}
			if (maximumDelay < minimumDelay) {
				TimeSpan temp = maximumDelay;
				maximumDelay = minimumDelay;
				minimumDelay = temp;
			}

			_minimumDelayMs = (int)Math.Floor(minimumDelay.TotalMilliseconds);
			_maximumDelayMs = (int)Math.Floor(maximumDelay.TotalMilliseconds);
		}

		/// <summary>
		/// Determine the delay before attempting to retry processing of a message
		/// </summary>
		/// <param name="failedLock">The last <seealso cref="IHandlerLock"/> which could not be acquired</param>
		/// <param name="message">Message being processed</param>
		/// <returns>Delay before retrying processing of the current message</returns>
		public TimeSpan GetMessageRetryInterval(IHandlerLock failedLock, Message message) {
			return TimeSpan.FromMilliseconds(_random.Next(_minimumDelayMs, _maximumDelayMs));
		}
	}
}