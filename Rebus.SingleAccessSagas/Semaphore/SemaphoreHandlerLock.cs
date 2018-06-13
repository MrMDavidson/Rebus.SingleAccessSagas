using System;
using System.Threading.Tasks;


namespace Rebus.SingleAccessSagas.Semaphore {
	/// <summary>
	/// An implementation of <seealso cref="IHandlerLock"/> which uses a machine wide <seealso cref="System.Threading.Semaphore"/> to provide single access saga controls to a single machine. If you have multiple worker machines you will need to use a distributed locking mechanism
	/// </summary>
	public class SemaphoreHandlerLock : IHandlerLock {
		private readonly int _operationCost;
		private readonly System.Threading.Semaphore _semaphore;
		private int _acquired = 0;

		/// <summary>
		/// Constructs a new instance of the lock and creates a system wide semaphore named <paramref name="semaphoreName"/>
		/// </summary>
		public SemaphoreHandlerLock(string semaphoreName, int maxConcurrency = 1, int operationCost = 1) {
			_operationCost = operationCost;
			_semaphore = new System.Threading.Semaphore(maxConcurrency, maxConcurrency, semaphoreName);
		}

		/// <summary>
		/// Dispose of any resources and free the semaphore if it has been acquired
		/// </summary>
		public void Dispose() {
			if (_acquired > 0) {
				_semaphore.Release(_acquired);
			}
			_semaphore.Dispose();
		}

		/// <summary>
		/// Attempt to acquire a lock. If the lock was successfully acquired return <c>true</c>. If the lock could not be acquired returns <c>false</c>
		/// </summary>
		public Task<bool> TryAcquire() {
			if (_acquired < _operationCost) {
				for (int request = _acquired; request < _operationCost; request++) {
					if (_semaphore.WaitOne(TimeSpan.FromMilliseconds(50)) == false) {
						break;
					}

					_acquired++;
				}
			}

			return Task.FromResult(_acquired == _operationCost);
		}
	}
}