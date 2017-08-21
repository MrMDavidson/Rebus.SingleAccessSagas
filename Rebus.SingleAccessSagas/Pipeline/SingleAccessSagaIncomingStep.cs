﻿using System;
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
	public class SingleAccessSagaIncomingStep : IIncomingStep {
		private static readonly Type SingleAccessSagaType = typeof(ISingleAccessSaga);
		private static readonly Type OpenSagaType = typeof(Saga<>);

		private readonly ILog _log;
		private readonly Lazy<IBus> _bus;
		private readonly ISagaLockProvider _sagaLockProvider;
		private readonly ISagaStorage _sagaStorage;
		private readonly ISagaLockRetryStrategy _retryStrategy;

		/// <summary>
		/// Constructs the step
		/// </summary>
		public SingleAccessSagaIncomingStep(ILog log, Func<IBus> busFactory, ISagaLockProvider sagaLockProvider, ISagaStorage sagaStorage, ISagaLockRetryStrategy retryStrategy) {
			_log = log;
			_bus = new Lazy<IBus>(busFactory, true);
			_sagaLockProvider = sagaLockProvider;
			_sagaStorage = sagaStorage;
			_retryStrategy = retryStrategy;
		}

		/// <summary>
		/// Inspects the message being processed and if any of its handlers have requested single access to the saga then acquires a lock on the saga. If a lock cannot be acquired the message is deferred until a later time.
		/// </summary>
		public async Task Process(IncomingStepContext context, Func<Task> next) {
			List<HandlerInvoker> singleAccessHandlers = context.Load<HandlerInvokers>()
				.Where(hi => hi.HasSaga)
				.Where(hi => SingleAccessSagaType.IsInstanceOfType(hi.Handler))
				.ToList();

			if (singleAccessHandlers.Any() == false) {
				await next();
				return;
			}

			SagaHelper helper = new SagaHelper();
			Message message = context.Load<Message>();
			object body = message.Body;

			_log.Debug($"{message.GetMessageLabel()} has {singleAccessHandlers.Count} single access message handlers");


			List<ISagaLock> locks = new List<ISagaLock>(singleAccessHandlers.Count);
			bool allLocksAcquired = true;

			try {
				ISagaLock failedLock = null;
				foreach (HandlerInvoker sagaInvoker in singleAccessHandlers) {
					SagaDataCorrelationProperties props = helper.GetCorrelationProperties(body, sagaInvoker.Saga);
					IEnumerable<CorrelationProperty> propsForMessage = props.ForMessage(body);
					Type sagaDataType = GetSagaDataType(sagaInvoker.Saga);
					if (sagaDataType == null) {
						_log.Error($"Could not extract the SagaData type from {sagaInvoker.Saga?.GetType()}. Aborting.");
						throw new ArgumentException($"Could not extract the SagaData type from {sagaInvoker.Saga?.GetType()}. Aborting.");
					}

					foreach (CorrelationProperty correlationProperty in propsForMessage) {
						object correlationId = correlationProperty.ValueFromMessage(MessageContext.Current, body);
						ISagaData sagaData = await _sagaStorage.Find(sagaDataType, correlationProperty.PropertyName, correlationId);
						if ((sagaData == null) && (sagaInvoker.CanBeInitiatedBy(body.GetType()) == false)) {
							_log.Debug($"No saga data for {message.GetMessageLabel()} and the saga cannot be initiated by this message. Skipping lock.");
							continue;
						}

						ISagaLock slock = await _sagaLockProvider.LockFor(correlationId);
						locks.Add(slock);
						if (await slock.TryAcquire() == true) {
							continue;
						}

						_log.Debug($"{message.GetMessageLabel()} could not acquire a saga lock for {correlationId} to process {sagaInvoker.Handler.GetType().FullName}");
						allLocksAcquired = false;
						failedLock = slock;
						break;
					}
				}

				if (allLocksAcquired == true) {
					await next();
				} else {
					_log.Info($"{message.GetMessageLabel()} could not have all required locks acquired. Deferring for later processing");

					TimeSpan retryInterval = _retryStrategy.GetMessageRetryInterval(failedLock, message);
					await _bus.Value.Advanced.TransportMessage.Defer(retryInterval);
				}
			} catch (Exception ex) {
				_log.Error(ex, "Error during processing of single access sagas - will revert any locks");
				throw;
			} finally {
				foreach (ISagaLock slock in locks) {
					slock.Dispose();
				}
			}
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