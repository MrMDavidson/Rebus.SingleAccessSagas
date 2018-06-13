using System.Threading.Tasks;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Provides a lock for a given <seealso cref="ConcurrencyControlInfo"/>
	/// </summary>
	public interface IHandlerLockProvider {
		/// <summary>
		/// Returns a <seealso cref="IHandlerLock"/> representing a lock for <paramref name="lockInfo"/>
		/// </summary>
		/// <param name="lockInfo">Details of the lock; name and concurrency requirements</param>
		/// <returns>A <seealso cref="IHandlerLock"/></returns>
		Task<IHandlerLock> LockFor(ConcurrencyControlInfo lockInfo);
	}
}