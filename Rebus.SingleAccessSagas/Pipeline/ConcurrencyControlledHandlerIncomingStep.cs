using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

namespace Rebus.SingleAccessSagas.Pipeline {
	/// <summary>
	/// Incoming pipeline step that checks to see if concurrency controls are in place for the message being processed. If they are appropriate locks are taken. Messages which cannot have all locks acquired are deferred.
	/// </summary>
	[StepDocumentation(@"Checks to see if a message requires concurrency controls. If they are then locks are acquired for each handler the message will encounter. If all locks are not acquired then the message will be deferred for later processing.

Note: this may cause message reordering")]
	public class ConcurrencyControlledHandlerIncomingStep : BaseLimitedAccessIncomingStep<IHandlerLock> {
		private static readonly Type HandleConcurrencyControlledMessagesType = typeof(IHandleConcurrencyControlledMessages);
		private static readonly Type OpenHandleConcurrencyControlledMessagesType = typeof(IHandleConcurrencyControlledMessages<>);
		private static readonly ConcurrentDictionary<Type, Func<IHandleConcurrencyControlledMessages, object, ConcurrencyControlInfo>> GetGetConcurrencyControlInfoForMessageCache = new ConcurrentDictionary<Type, Func<IHandleConcurrencyControlledMessages, object, ConcurrencyControlInfo>>();

		private readonly IHandlerLockProvider _handlerLockProvider;

		/// <summary>
		/// Constructs the pipeline step
		/// </summary>
		/// <param name="busFactory">Factory for creating the bus instance</param>
		/// <param name="log">Log for writing debug/informational messages</param>
		/// <param name="handlerLockProvider">Provider for constructing specific locks as required</param>
		/// <param name="retryStrategy">Strategy for deferring messages when they were not able to acquire their locks</param>
		public ConcurrencyControlledHandlerIncomingStep(Func<IBus> busFactory, ILog log, IHandlerLockProvider handlerLockProvider, IHandlerLockRetryStrategy retryStrategy) : base(busFactory, log, retryStrategy) {
			_handlerLockProvider = handlerLockProvider;
		}

		/// <inheritdoc />
		protected override async Task<bool> TryAcquireLocksForHandler(HandlerInvoker invoker, Message message, IncomingStepContext context, IList<IHandlerLock> locks) {
			Type messageBodyType = message.Body.GetType();
			//Type invokerType = GetHandlerType(invoker.Handler.GetType(), messageBodyType);
			Func<IHandleConcurrencyControlledMessages, object, ConcurrencyControlInfo> getConcurrencyControlInfoForMessage = GetGetConcurrencyControlInfoForMessageCache.GetOrAdd(messageBodyType, BuildExpression);

			// Verify the concurrency requirement exists
			ConcurrencyControlInfo accessInfo = getConcurrencyControlInfoForMessage(invoker.Handler as IHandleConcurrencyControlledMessages, message.Body);
			if (accessInfo == null) {
				Log.Debug($"{message.GetMessageLabel()} returned NULL when concurrency info. Will treat as if no concurrency controls are required.");
				return true;
			}

			// Create the lock of the concurrency requirement
			IHandlerLock handlerLock = await _handlerLockProvider.LockFor(accessInfo);
			locks.Add(handlerLock);

			// Attempt to acquire the lock
			if (await handlerLock.TryAcquire() == false) {
				// Fail if it cannot be acquired
				Log.Debug($"{message.GetMessageLabel()} could not acquire a lock for {invoker.Handler.GetType().FullName}.");
				return false;
			}

			return true;
		}

		/// <summary>
		/// Returns the subset of handlers which are marked as implementing <seealso cref="IHandleConcurrencyControlledMessages"/>
		/// </summary>
		protected override IList<HandlerInvoker> GetInvokersRequiringLocks(IncomingStepContext context) {
			return base.GetInvokersRequiringLocks(context)
				.Where(hi => HandleConcurrencyControlledMessagesType.IsInstanceOfType(hi.Handler))
				.ToList();
		}

		private Type GetHandlerType(Type handlerType, Type messageType) {
			foreach (Type implementedInterface in handlerType.GetInterfaces()) {
				if ((implementedInterface.IsConstructedGenericType == true) && (implementedInterface.GetGenericTypeDefinition() == OpenHandleConcurrencyControlledMessagesType)) {
					Type[] typeArguments = implementedInterface.GenericTypeArguments;

					if (typeArguments.First() == messageType) {
						return implementedInterface;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Creates a strongly typed invocation of <seealso cref="IHandleConcurrencyControlledMessages{TMessageType}.GetConcurrencyControlInfoForMessage"/> for <paramref name="messageBodyType"/>
		/// </summary>
		private Func<IHandleConcurrencyControlledMessages, object, ConcurrencyControlInfo> BuildExpression(Type messageBodyType) {
			// Create the closed generic for the message type; IHandleConcurrencyControlledMessages<> => IHandleConcurrencyControlledMessages<MessageType>
			Type handlerType = OpenHandleConcurrencyControlledMessagesType.MakeGenericType(messageBodyType);

			// Params to the lambda (handler, message) => ...
			ParameterExpression instanceExpr = Expression.Parameter(HandleConcurrencyControlledMessagesType, "handler");
			ParameterExpression messageExpr = Expression.Parameter(typeof(object), "message");

			// Cast from generic values to the appropriate types
			UnaryExpression handlerCastExpr = Expression.Convert(instanceExpr, handlerType);
			UnaryExpression messageCastExpr = Expression.Convert(messageExpr, messageBodyType);

			// ((IHandleMessages<MessageType>)handler).GetConcurrencyControlInfoForMessage((MessageType)message)
			MethodCallExpression callExpr = Expression.Call(
				handlerCastExpr,
				handlerType.GetMethod("GetConcurrencyControlInfoForMessage"),
				messageCastExpr
			);

			// (handler, message) => ((IHandleMessages<MessageType>)handler).GetConcurrencyControlInfoForMessage((MessageType)message)
			Expression<Func<IHandleConcurrencyControlledMessages, object, ConcurrencyControlInfo>> lambdaExpr = Expression.Lambda<Func<IHandleConcurrencyControlledMessages, object, ConcurrencyControlInfo>>(
				callExpr,
				instanceExpr,
				messageExpr
			);

			// Compile the lambda into a Func<> for use
			return lambdaExpr.Compile();
		}
	}
}