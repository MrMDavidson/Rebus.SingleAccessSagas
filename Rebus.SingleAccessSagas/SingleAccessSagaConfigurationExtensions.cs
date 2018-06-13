﻿using System;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Sagas;
using Rebus.SingleAccessSagas.Pipeline;
using Rebus.SingleAccessSagas.Semaphore;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Configuration extension for enabling single access saga feature. This feature ensures only one message from a saga is processed
	/// at a time.
	/// </summary>
	public static class SingleAccessSagaConfigurationExtensions {
		/// <summary>
		/// Enables single access sagas. When enabled saga handlers can be tagged with <seealso cref="ISingleAccessSaga"/> which will
		/// require pessimistic locks to be taken to ensure mutual exclusion whilst executing all handlers for the saga. This is useful
		/// if you have sagas that are chatty, have a high number of workers/threads, but processing of the saga uses valuable resources
		/// (CPU, cost, limited number of external API calls) and you want to ensure that a <seealso cref="Rebus.Exceptions.ConcurrencyException"/>
		/// is not thrown causing resources to be wasted
		/// </summary>
		/// <remarks>Note: If you have multiple worker machines you will need to register a suitable <seealso cref="ISagaLockProvider"/></remarks>
		public static void EnableSingleAccessSagas(this OptionsConfigurer configurer) {
			if (configurer.Has<ISagaLockProvider>() == false) {
				configurer.Register<ISagaLockProvider>(res => new SemaphoreSagaLockProvider());
			}
			if (configurer.Has<IHandlerLockRetryStrategy>() == false) {
				configurer.Register<IHandlerLockRetryStrategy>(res => new RandomJitterHandlerLockRetryStrategy(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)));
			}

			configurer.Decorate<IPipeline>(
				c => {
					IRebusLoggerFactory logger = c.Get<IRebusLoggerFactory>();
					IPipeline pipeline = c.Get<IPipeline>();
					PipelineStepInjector injector = new PipelineStepInjector(pipeline);
					ISagaStorage storage = c.Get<ISagaStorage>();
					ISagaLockProvider lockProvider = c.Get<ISagaLockProvider>();
					Func<IBus> busFactory = c.Get<IBus>;
					IHandlerLockRetryStrategy handlerLockRetryStrategy = c.Get<IHandlerLockRetryStrategy>();

					SingleAccessSagaIncomingStep incomingStep = new SingleAccessSagaIncomingStep(logger.GetLogger<SingleAccessSagaIncomingStep>(), busFactory, lockProvider, storage, handlerLockRetryStrategy);
					injector.OnReceive(incomingStep, PipelineRelativePosition.Before, typeof(LoadSagaDataStep));

					return injector;
				}
			);
		}
	}
}