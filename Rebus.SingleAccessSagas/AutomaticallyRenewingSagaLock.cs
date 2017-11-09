using System;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Exceptions;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Deferring implemtnation of <seealso cref="ISagaLock"/> which will automatically re-acquire the lock periodically. Useful for lease based locks
	/// </summary>
	public class AutomaticallyRenewingSagaLock : ISagaLock {
		private readonly ISagaLock _actualLock;
		private readonly TimeSpan _reacquistionInterval;
		private readonly Timer _lockRenewalTimer;
		private bool _acquiredLock = false;

		/// <summary>
		/// Wraps around another instance of a <seealso cref="ISagaLock"/> and automatically renews the lock periodically
		/// </summary>
		/// <param name="actualLock">An instance of a <seealso cref="ISagaLock"/> which performs the actual lock acquisition</param>
		/// <param name="reacquistionInterval">Period of time before the lock will be reacquired automatically</param>
		public AutomaticallyRenewingSagaLock(ISagaLock actualLock, TimeSpan reacquistionInterval) {
			_actualLock = actualLock;
			_reacquistionInterval = reacquistionInterval;

			_lockRenewalTimer = new Timer(RenewLock, null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
		}

		/// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
		public void Dispose() {
			_lockRenewalTimer?.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
			_actualLock?.Dispose();
		}

		/// <summary>
		/// Attempt to acquire a lock. If the lock was successfully acquired return <c>true</c>. If the lock could not be acquired returns <c>false</c>
		/// </summary>
		public async Task<bool> TryAcquire() {
			_acquiredLock = await _actualLock.TryAcquire();
			if (_acquiredLock == true) {
				_lockRenewalTimer.Change(_reacquistionInterval, _reacquistionInterval);
			}

			return _acquiredLock;
		}

		private async void RenewLock(object state) {
			if (await _actualLock.TryAcquire() == false) {
				throw new ConcurrencyException("Failed when attempting to re-acquire lock");
			}
		}
	}
}