using System;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Sagas;
using Rebus.SingleAccessSagas.Pipeline;
using Rebus.SingleAccessSagas.Semaphore;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Configuration extension for enabling concurrency controlled handlers. This feature limits the number of concurrent handlers that can fire.
	///
	/// This can be useful to provide infrastructure level throttling. For example; you might limit the number of concurrent operations a customre can perform. Or you might have an external API that you want to ensure you do not flood
	/// </summary>
    public static class ConcurrencyControlledHandlingExtentions {
		/// <summary>
		/// Enables concurrency controlled handlers. When enabled handlers deriving from <seealso cref="IHandleConcurrencyControlledMessages{TMessageType}"/> will have their lock requirements interrogated (via <seealso cref="IHandleConcurrencyControlledMessages{TMessageType}.GetConcurrencyControlInfoForMessage"/> and appropriate locks will be acquired via the registered <seealso cref="IHandlerLockProvider"/>. If all locks cannot be acquired the message will be deferred.
		/// </summary>
		/// <remarks>Note: If you have multiple worker machines you will need to register a suitable <seealso cref="IHandlerLockProvider"/> which performs distributed locking (eg. Using Redis, Azure Leases, Red/Black locks, etc). The default implementation uses a machine wide semaphore.</remarks>
	    public static void EnableConcurrencyControlledHandling(this OptionsConfigurer configurer) {
		    if (configurer.Has<IHandlerLockProvider>() == false) {
				configurer.Register<IHandlerLockProvider>(res => new SemaphoreHandlerLockProvider());
		    }

		    configurer.Decorate<IPipeline>(
			    c => {
				    IRebusLoggerFactory logger = c.Get<IRebusLoggerFactory>();
				    IPipeline pipeline = c.Get<IPipeline>();
				    PipelineStepInjector injector = new PipelineStepInjector(pipeline);
				    IHandlerLockProvider lockProvider = c.Get<IHandlerLockProvider>();
				    Func<IBus> busFactory = c.Get<IBus>;
				    ISagaLockRetryStrategy sagaLockRetryStrategy = c.Get<ISagaLockRetryStrategy>();

				    ConcurrencyControlledHandlerIncomingStep incomingStep = new ConcurrencyControlledHandlerIncomingStep(busFactory, logger.GetLogger<ConcurrencyControlledHandlerIncomingStep>(), lockProvider, sagaLockRetryStrategy);
				    injector.OnReceive(incomingStep, PipelineRelativePosition.Before, typeof(LoadSagaDataStep));

				    return injector;
			    }
		    );
	    }
    }
}
