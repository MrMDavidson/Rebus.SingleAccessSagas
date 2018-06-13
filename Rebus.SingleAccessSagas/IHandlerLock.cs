using System;
using System.Threading.Tasks;
using Rebus.Handlers;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Defines an interface for acquiring a lock for processing a <seealso cref="IHandleMessages"/> of some kind. Implementors should release the lock, if acquired, on disposal
	/// </summary>
	public interface IHandlerLock : IDisposable {
		/// <summary>
		/// Attempt to acquire a lock. If the lock was successfully acquired return <c>true</c>. If the lock could not be acquired returns <c>false</c>
		/// </summary>
		Task<bool> TryAcquire();
	}
}