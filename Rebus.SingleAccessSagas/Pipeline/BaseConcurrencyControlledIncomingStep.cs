using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

namespace Rebus.SingleAccessSagas.Pipeline {
	/// <summary>
	/// Base class for any kind of concurrency controlled handling.
	/// </summary>
	public class BaseConcurrencyControlledIncomingStep<TLockType> : IIncomingStep where TLockType : IDisposable {
		/// <summary>
		/// Log instance for the step
		/// </summary>
		protected ILog Log { get; }

		private readonly Lazy<IBus> _bus;
		private readonly IHandlerLockRetryStrategy _retryStrategy;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="busFactory">A factory that provides a <see cref="IBus"/>. Will not be constructed until a message is processed.</param>
		/// <param name="log">Logger for the step</param>
		/// <param name="retryStrategy">Strategy that can determine when to reschedule a message when a message cannot have all of its locks acquired</param>
		protected BaseConcurrencyControlledIncomingStep(Func<IBus> busFactory, ILog log, IHandlerLockRetryStrategy retryStrategy) {
			Log = log;
			_bus = new Lazy<IBus>(busFactory, true);
			_retryStrategy = retryStrategy;
		}

		/// <inheritdoc />
		public async Task Process(IncomingStepContext context, Func<Task> next) {
			IList<HandlerInvoker> handlers = GetInvokersRequiringLocks(context);

			if (handlers.Any() == false) {
				await next();
				return;
			}

			Message message = context.Load<Message>();
			List<TLockType> locks = new List<TLockType>(handlers.Count);
			bool acquiredAllLocks = true;

			Log.Debug($"{message.GetMessageLabel()} has {handlers.Count} limited access handlers.");

			try {
				TimeSpan retryInterval = TimeSpan.FromSeconds(5);

				foreach (HandlerInvoker handler in handlers) {
					if (await TryAcquireLocksForHandler(handler, message, context, locks) == false) {
						acquiredAllLocks = false;

						retryInterval = _retryStrategy.GetMessageRetryInterval(handler, message, context);

						break;
					}
				}

				if (acquiredAllLocks == true) {
					await next();
				} else {
					Log.Info($"{message.GetMessageLabel()} could not acquire all required locks for processing. Deferring for later processing.");
					await _bus.Value.Advanced.TransportMessage.Defer(retryInterval);
				}

			} catch (Exception ex) {
				Log.Error(ex, "Error during processing of limited access handling - will revert any locks.");
				throw;
			} finally {
				foreach (TLockType slock in locks) {
					slock.Dispose();
				}
			}
		}

		/// <summary>
		/// Retrieve all of the <see cref="HandlerInvokers"/> for the given context. The base implementation returns all handlers. Derived classes are likely to provide a subset of these invokers - for example those implementing a marker interface
		/// </summary>
		/// <param name="context">The context of the step being executed</param>
		/// <remarks>Each <see cref="HandlerInvoker"/> returned here will be passed to <seealso cref="TryAcquireLocksForHandler"/> in order. If any fail to fail to acquire their locks further handlers are not locked.</remarks>
		/// <returns>A <seealso cref="IList{T}"/> of <seealso cref="HandlerInvoker">HandlerInvoker</seealso> that require locks to be acquired</returns>
		protected virtual IList<HandlerInvoker> GetInvokersRequiringLocks(IncomingStepContext context) {
			return context.Load<HandlerInvokers>()
				.ToList();
		}

		/// <summary>
		/// Attempt to acqwuire a lock for the current handler and message
		/// </summary>
		/// <param name="invoker">Invoker for the message</param>
		/// <param name="message">Message being processed</param>
		/// <param name="context">The context under which the step is executing</param>
		/// <param name="locks">Any locks acquired as a result of this invoker should be </param>
		/// <returns><c>true</c> if all required locks were acquired. <c>false</c> if any could not be acquired. If any failed further processing will be aborted</returns>
		protected virtual Task<bool> TryAcquireLocksForHandler(HandlerInvoker invoker, Message message, IncomingStepContext context, IList<TLockType> locks) {
			return Task.FromResult(true);
		}
	}
}