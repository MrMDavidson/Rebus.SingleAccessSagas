using System;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

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
		
		/// <inheritdoc />
		public TimeSpan GetMessageRetryInterval(HandlerInvoker unlockedHandler, Message message, IncomingStepContext context){
			return TimeSpan.FromMilliseconds(_random.Next(_minimumDelayMs, _maximumDelayMs));
		}
	}
}