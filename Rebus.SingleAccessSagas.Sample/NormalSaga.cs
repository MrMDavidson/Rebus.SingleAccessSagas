using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Rebus.SingleAccessSagas.Sample {
	public class NormalSaga : Saga<NormalSaga.CountingSagaData>,
		IAmInitiatedBy<NormalSaga.StartSagaCommand>,
		IHandleMessages<NormalSaga.IncrementCounterCommand> {
		private readonly IBus _bus;

		public class StartSagaCommand {
			public Guid Id { get; set; }
			public int NumberOfMessages { get; set; }
		}

		public class IncrementCounterCommand {
			public Guid Id { get; set; }
		}

		public class CountingSagaData : SagaData {
			public int NumberOfMessages { get; set; }
			public int ReceivedMessages { get; set; }
		}

		public NormalSaga(IBus bus) {
			_bus = bus;
		}

		/// <summary>
		/// This method must be implemented in order to configure correlation of incoming messages with existing saga data instances.
		///             Use the injected <see cref="T:Rebus.Sagas.ICorrelationConfig`1"/> to set up the correlations, e.g. like so:
		/// <code>
		/// config.Correlate&lt;InitiatingMessage&gt;(m =&gt; m.OrderId, d =&gt; d.CorrelationId);
		///             config.Correlate&lt;CorrelatedMessage&gt;(m =&gt; m.CorrelationId, d =&gt; d.CorrelationId);
		/// </code>
		/// </summary>
		protected override void CorrelateMessages(ICorrelationConfig<CountingSagaData> config) {
			config.Correlate<StartSagaCommand>(x => x.Id, x => x.Id);
			config.Correlate<IncrementCounterCommand>(x => x.Id, x => x.Id);
		}

		/// <summary>
		/// This method will be invoked with a message of type <typeparamref name="TMessage"/>
		/// </summary>
		public async Task Handle(StartSagaCommand message) {
			if (Data.NumberOfMessages != 0) {
				return;
			}

			Data.Id = message.Id;
			Data.NumberOfMessages = message.NumberOfMessages;

			for (int i = 0; i < Data.NumberOfMessages; i++) {
				await _bus.Send(new IncrementCounterCommand() { Id = Data.Id });
			}
		}

		/// <summary>
		/// This method will be invoked with a message of type <typeparamref name="TMessage"/>
		/// </summary>
		public Task Handle(IncrementCounterCommand message) {
			Data.ReceivedMessages++;

			return CheckAndMarkSagaForCompletion();
		}

		private Task CheckAndMarkSagaForCompletion() {
			if (Data.NumberOfMessages == Data.ReceivedMessages) {
				Console.WriteLine("Saga {0} has completed!", Data.Id);
				MarkAsComplete();
			}

			return Task.FromResult(0);
		}
	}
}