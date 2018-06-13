using System.Threading.Tasks;

namespace Rebus.SingleAccessSagas.Semaphore {
	/// <summary>
	/// Implements <seealso cref="IHandlerLockProvider"/> using global semaphores
	/// </summary>
	public class SemaphoreHandlerLockProvider : IHandlerLockProvider {
		/// <summary>
		/// Provide a <seealso cref="SemaphoreHandlerLock"/> for <paramref name="lockInfo"/>
		/// </summary>
		public Task<IHandlerLock> LockFor(ConcurrencyControlInfo lockInfo) {
			return Task.FromResult<IHandlerLock>(new SemaphoreHandlerLock($"rbs2-limited-access-handler-lock-{lockInfo.LockIdentifier}", lockInfo.MaxConcurrency, lockInfo.OperationCost));
		}
	}
}