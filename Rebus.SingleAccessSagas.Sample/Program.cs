using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Persistence.FileSystem;
using Rebus.Retry.Simple;
using Rebus.Routing;
using Rebus.SqlServer;
using Rebus.SqlServer.Transport;
using Rebus.Threading;
using Rebus.Transport.FileSystem;

namespace Rebus.SingleAccessSagas.Sample {
	class Program {
		static void Main(string[] args) {
			string inputQueueName = "Test.Input";
			string errorQueueName = "Test.Error";

			using (BuiltinHandlerActivator handlerActivator = new BuiltinHandlerActivator()) {
				RebusConfigurer inputConfig = ConfigureBus(handlerActivator, args[0], inputQueueName, errorQueueName, false);
				using (IBus bus = inputConfig.Start()) {
					handlerActivator.Register<IHandleMessages<NormalSaga.StartSagaCommand>>(() => new NormalSaga(bus));
					handlerActivator.Register<IHandleMessages<NormalSaga.IncrementCounterCommand>>(() => new NormalSaga(bus));

					handlerActivator.Register<IHandleMessages<SingleAccessSaga.StartSagaCommand>>(() => new SingleAccessSaga(bus));
					handlerActivator.Register<IHandleMessages<SingleAccessSaga.IncrementCounterCommand>>(() => new SingleAccessSaga(bus));

					handlerActivator.Register<IHandleMessages<LimitedAccessSaga.StartSagaCommand>>(() => new LimitedAccessSaga(bus));
					handlerActivator.Register<IHandleMessages<LimitedAccessSaga.IncrementCounterCommand>>(() => new LimitedAccessSaga(bus));

					handlerActivator.Register<IHandleMessages<LimitAccessHandler.PingCommand>>(() => new LimitAccessHandler());

					bus.Advanced.Topics.Subscribe(typeof(NormalSaga.StartSagaCommand).GetSimpleAssemblyQualifiedName());
					bus.Advanced.Topics.Subscribe(typeof(NormalSaga.IncrementCounterCommand).GetSimpleAssemblyQualifiedName());

					bus.Advanced.Topics.Subscribe(typeof(SingleAccessSaga.StartSagaCommand).GetSimpleAssemblyQualifiedName());
					bus.Advanced.Topics.Subscribe(typeof(SingleAccessSaga.IncrementCounterCommand).GetSimpleAssemblyQualifiedName());

					bus.Advanced.Topics.Subscribe(typeof(LimitedAccessSaga.StartSagaCommand).GetSimpleAssemblyQualifiedName());
					bus.Advanced.Topics.Subscribe(typeof(LimitedAccessSaga.IncrementCounterCommand).GetSimpleAssemblyQualifiedName());

					bus.Advanced.Topics.Subscribe(typeof(LimitAccessHandler.PingCommand).GetSimpleAssemblyQualifiedName());


					void DisplayHelp() {
						Console.WriteLine("Bus has started. Press any of the following keys");
						Console.WriteLine("1 = Add Worker");
						Console.WriteLine("2 = Remove Worker");
						Console.WriteLine("3 = Launch another instance");
						Console.WriteLine("5 = Start Normal Saga");
						Console.WriteLine("6 = Start Single Access Saga");
						Console.WriteLine("7 = Start Limited Access Saga");
						Console.WriteLine("8 = Spam limited access handler");
						Console.WriteLine("? = Display this help");
						Console.WriteLine("Q = Quit");
					}
					DisplayHelp();

					List<Process> spawnedProcesses = new List<Process>();

					ConsoleKeyInfo input = Console.ReadKey();
					while (input.KeyChar != 'Q') {
						Console.WriteLine();
						switch (input.KeyChar) {
							case '?': {
								DisplayHelp();
								break;
							}
							case '1': {
								bus.Advanced.Workers.SetNumberOfWorkers(bus.Advanced.Workers.Count + 1);
								Console.WriteLine("Now running with {0} workers", bus.Advanced.Workers.Count);

								break;
							}
							case '2': {
								if (bus.Advanced.Workers.Count > 0) {
									bus.Advanced.Workers.SetNumberOfWorkers(bus.Advanced.Workers.Count - 1);
									Console.WriteLine("Now running with {0} workers", bus.Advanced.Workers.Count);
								} else {
									Console.WriteLine("Already have 0 workers.");
								}

								break;
							}
							case '3': {
								ProcessStartInfo startInfo = new ProcessStartInfo(System.Reflection.Assembly.GetEntryAssembly().Location, string.Join(" ", args.Select(a => $"\"{a}\"")));
								Process process = Process.Start(startInfo);
								process.Exited += (sender, eventArgs) => { spawnedProcesses.Remove(process); };
								spawnedProcesses.Add(process);

								Console.WriteLine("Spawneda new instance");

								break;
							}
							case '5': {
								bus.Send(new NormalSaga.StartSagaCommand() { Id = Guid.NewGuid(), NumberOfMessages = 100 })
									.Wait();
								break;
							}
							case '6': {
								bus.Send(new SingleAccessSaga.StartSagaCommand() { Id = Guid.NewGuid(), NumberOfMessages = 100 })
									.Wait();
								break;
							}
							case '7': {
								bus.Send(new LimitedAccessSaga.StartSagaCommand() { Id = Guid.NewGuid(), NumberOfMessages = 100 })
									.Wait();
								break;
							}

							case '8': {
								Parallel.For(0, 100, (x) => { bus.Send(new LimitAccessHandler.PingCommand()).Wait(); }
								);

								//bus.Send(new LimitedAccessSaga.StartSagaCommand() { Id = Guid.NewGuid(), NumberOfMessages = 100 })
								//	.Wait();
								break;
							}
						}

						input = Console.ReadKey();
					}

					Console.WriteLine("Quitting");

					if (spawnedProcesses.Count > 0) {
						Console.WriteLine($"Shutting down {spawnedProcesses.Count} other instances");
						foreach (Process process in spawnedProcesses.ToArray()) {
							if (process.HasExited == false) {
								process.Kill();
								process.WaitForExit();
							}

							process.Dispose();
						}
						Console.WriteLine("Done");
					}
				}
			}

			Console.ReadKey();
		}

		private static RebusConfigurer ConfigureBus(IHandlerActivator activator, string sagaDatabaseConnectionString, string inputQueueName, string errorQueueName, bool transformToErrors = false) {
			RebusConfigurer config = Configure
				.With(activator)
				.Logging(x => x.ColoredConsole());

			if (string.IsNullOrWhiteSpace(sagaDatabaseConnectionString) == true) {
				config.Transport(x => x.UseFileSystem(Path.Combine(Environment.CurrentDirectory, "Queue"), inputQueueName));
				config.Subscriptions(x => x.UseJsonFile(Path.Combine(Environment.CurrentDirectory, "subscription.json")));
				config.Sagas(x => x.UseFilesystem(Path.Combine(Environment.CurrentDirectory, "Sagas")));
				config.Timeouts(x => x.UseFileSystem(Path.Combine(Environment.CurrentDirectory, "Timeouts")));
			} else {
				config.Transport(x => x.Register(res => new SqlServerLeaseTransport(new DbConnectionProvider(sagaDatabaseConnectionString, res.Get<IRebusLoggerFactory>()), "RebusMessage", inputQueueName, res.Get<IRebusLoggerFactory>(), res.Get<IAsyncTaskFactory>(), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), () => $"{Environment.MachineName}, PID: {Process.GetCurrentProcess().Id}", TimeSpan.FromMinutes(2))));
				//config.Transport(x => x.Register(res => new SqlServerTransport(new DbConnectionProvider(sagaDatabaseConnectionString, res.Get<IRebusLoggerFactory>()), "RebusMessage", inputQueueName)));
				config.Subscriptions(x => x.StoreInSqlServer(sagaDatabaseConnectionString, "RebusSubscription", automaticallyCreateTables: false));
				config.Sagas(x => x.StoreInSqlServer(sagaDatabaseConnectionString, "RebusSaga", "RebusSagaIndex", automaticallyCreateTables: false));
				config.Timeouts(x => x.StoreInSqlServer(sagaDatabaseConnectionString, "RebusTimeout", automaticallyCreateTables: false));
			}

			config.Options(o => o.SimpleRetryStrategy(errorQueueName, maxDeliveryAttempts: 5));

			config.Options(o => o.EnableSingleAccessSagas());
			config.Options(o => o.EnableConcurrencyControlledHandling());

			config.Routing(x => x.Register(res => new StaticRouter(inputQueueName)));

			config.Options(x => x.LogPipeline(verbose: true));

			config.Options(x => x.SetNumberOfWorkers(0));
			config.Options(x => x.SetMaxParallelism(10));

			return config;
		}
	}

	public class StaticRouter : IRouter {
		private readonly string _queue;

		public StaticRouter(string queue) {
			_queue = queue;
		}

		/// <summary>
		/// Called when sending messages
		/// </summary>
		public Task<string> GetDestinationAddress(Message message) {
			return Task.FromResult(_queue);
		}

		/// <summary>
		/// Called when subscribing to messages
		/// </summary>
		public Task<string> GetOwnerAddress(string topic) {
			return Task.FromResult(_queue);
		}
	}
}