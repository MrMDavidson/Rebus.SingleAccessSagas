using System;
using System.Threading.Tasks;

namespace Rebus.SingleAccessSagas {
	/// <summary>
	/// Defines an interface for acquiring a lock on a <seealso cref="Rebus.Sagas.Saga{TSagaData}"/>. Implementors should release the lock, if acquired, on disposal
	/// </summary>
	public interface ISagaLock : IDisposable {
		/// <summary>
		/// Attempt to acquire a lock. If the lock was successfully acquired return <c>true</c>. If the lock could not be acquired returns <c>false</c>
		/// </summary>
		Task<bool> TryAcquire();
	}
}