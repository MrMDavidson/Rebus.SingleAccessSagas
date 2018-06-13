using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Sagas;

namespace Rebus.SingleAccessSagas.Pipeline {
	/// <summary>
	/// Incoming pipeline step that checks to see if a single access saga is being processed. If it is locks are acquired for each of 
	/// the message handlers the saga will encounter before the pipeline proceeds. If they cannot be acquired then the message is 
	/// deferred
	/// </summary>
	[StepDocumentation(@"Checks to see if a message participates in sagas and if any of those are marked as being single access.

If they are then locks are acquired for each single access handler the message will encounter. If all locks are not acquired then the message will be deferred for later processing.

Note: this may cause message reordering")]
	public class SingleAccessSagaIncomingStep : BaseLimitedAccessIncomingStep<IHandlerLock> {
		private static readonly Type SingleAccessSagaType = typeof(ISingleAccessSaga);
		private static readonly Type OpenSagaType = typeof(Saga<>);

		private readonly ISagaLockProvider _sagaLockProvider;
		private readonly ISagaStorage _sagaStorage;
		private readonly SagaHelper _sagaHelper;

		/// <summary>
		/// Constructs the step
		/// </summary>
		public SingleAccessSagaIncomingStep(ILog log, Func<IBus> busFactory, ISagaLockProvider sagaLockProvider, ISagaStorage sagaStorage, ISagaLockRetryStrategy retryStrategy) 
			: base(busFactory, log, retryStrategy) {
			_sagaLockProvider = sagaLockProvider;
			_sagaStorage = sagaStorage;
			_sagaHelper = new SagaHelper();
		}

		/// <inheritdoc />
		protected override IList<HandlerInvoker> GetInvokersRequiringLocks(IncomingStepContext context) {
			return base.GetInvokersRequiringLocks(context)
				.Where(hi => hi.HasSaga)
				.Where(hi => SingleAccessSagaType.IsInstanceOfType(hi.Handler))
				.ToList();
		}

		/// <inheritdoc />
		protected override async Task<bool> TryAcquireLocksForHandler(HandlerInvoker invoker, Message message, IncomingStepContext context, IList<IHandlerLock> locks) {
			object body = message.Body;
			SagaDataCorrelationProperties props = _sagaHelper.GetCorrelationProperties(body, invoker.Saga);
			IEnumerable<CorrelationProperty> propsForMessage = props.ForMessage(body);
			Type sagaDataType = GetSagaDataType(invoker.Saga);
			if (sagaDataType == null) {
				Log.Error($"Could not extract the SagaData type from {invoker.Saga?.GetType()}. Aborting.");
				throw new ArgumentException($"Could not extract the SagaData type from {invoker.Saga?.GetType()}. Aborting.");
			}

			bool allLocksAcquired = true;
			foreach (CorrelationProperty correlationProperty in propsForMessage) {
				object correlationId = correlationProperty.ValueFromMessage(MessageContext.Current, body);
				ISagaData sagaData = await _sagaStorage.Find(sagaDataType, correlationProperty.PropertyName, correlationId);
				if ((sagaData == null) && (invoker.CanBeInitiatedBy(body.GetType()) == false)) {
					Log.Debug($"No saga data for {message.GetMessageLabel()} and the saga cannot be initiated by this message. Skipping lock.");
					continue;
				}

				IHandlerLock slock = await _sagaLockProvider.LockFor(correlationId);
				locks.Add(slock);
				if (await slock.TryAcquire() == true) {
					continue;
				}

				Log.Debug($"{message.GetMessageLabel()} could not acquire a saga lock for {correlationId} to process {invoker.Handler.GetType().FullName}");
				allLocksAcquired = false;
				break;
			}

			return allLocksAcquired;
		}


		/// <summary>
		/// When given a <seealso cref="Saga"/> will return the type of <seealso cref="Saga{TSagaData}"/>
		/// </summary>
		/// <param name="saga">An instance of a <seealso cref="Saga"/></param>
		/// <returns><seealso cref="Type"/> of the saga data for <paramref name="saga"/> or <c>null</c> if it cannot be determined</returns>
		private Type GetSagaDataType(Saga saga) {
			if (saga == null) {
				return null;
			}

			Type sagaType = saga.GetType();
			foreach (Type parent in sagaType.GetBaseTypes()) {
				if ((parent.GetTypeInfo().IsGenericType == true) && (parent.GetGenericTypeDefinition() == OpenSagaType)) {
					return parent.GenericTypeArguments.FirstOrDefault();
				}
			}

			return null;
		}
	}
}