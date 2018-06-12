using System;
using System.Threading.Tasks;


namespace Rebus.SingleAccessSagas.Semaphore {
	/// <summary>
	/// An implementation of <seealso cref="ISagaLock"/> which uses a machine wide <seealso cref="System.Threading.Semaphore"/> to provide single access saga controls to a single machine. If you have multiple worker machines you will need to use a distributed locking mechanism
	/// </summary>
	public class SemaphoreSagaLock : ISagaLock {
		private readonly System.Threading.Semaphore _semaphore;
		private bool _acquired = false;

		/// <summary>
		/// Constructs a new instance of the lock and creates a system wide semaphore named <paramref name="semaphoreName"/>
		/// </summary>
		public SemaphoreSagaLock(string semaphoreName, int maxConcurrency = 1) {
			_semaphore = new System.Threading.Semaphore(maxConcurrency, maxConcurrency, semaphoreName);
		}

		/// <summary>
		/// Dispose of any resources and free the semaphore if it has been acquired
		/// </summary>
		public void Dispose() {
			if (_acquired == true) {
				_semaphore.Release();
			}
			_semaphore.Dispose();
		}

		/// <summary>
		/// Attempt to acquire a lock. If the lock was successfully acquired return <c>true</c>. If the lock could not be acquired returns <c>false</c>
		/// </summary>
		public Task<bool> TryAcquire() {
			if (_acquired == false) {
				_acquired = _semaphore.WaitOne(TimeSpan.FromMilliseconds(50));
			}
			return Task.FromResult(_acquired);
		}
	}
}