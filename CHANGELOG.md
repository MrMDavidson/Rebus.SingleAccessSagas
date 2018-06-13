# Change Log

## 0.0.1-alpha6 to 0.1.0-alpha1

1. Introduced `ConcurrencyControlInfo`, This class describes concurrency requirements. `LockIdentifier` is analogous to the `sagaCorrelationId` argument to `ISagaLockProvider::LockFor`. It defines the "scope" of the concurrency requirement and should be treated as an opaque value. `MaxConcurrency` specifies how many concurrent operations for the given `LockIdentifier` are allowed. And `OperationCost` defines the cost of the current operation. If these values are equal (eg. 1 and 1) only a single operation can occur at once. If `MaxConcurrency` is greater than `OperationCost` than multiple operations may occur at once.
1. `ISagaLock` has been renamed to `IHandlerLock`. Interface remains the same
1. `ISagaLockProvider` has been replaced by `IHandlerLockProvider`. The new API takes a `ConcurrencyControlInfo`.
1. `SemaphoreSagaLockProvider` has been renamed to `SemaphoreHandlerLockProvider`
1. `SemaphoreSagaLock` has been renamed to `SemaphoreHandlerLock`
1. `AutomaticallyRenewingSagaLock` has been renamed to `AutomaticallyRenewingHandlerLock`
1. `ISagaLockRetryStrategy` has been renamed to `IHandlerLockRetryStrategy`. Interface remains the same.
1. `RandomJitterSagaLockRetryStrategy` has been renamed to `RandomJitterHandlerLockRetryStrategy`
1. Introduced `BaseConcurrencyControlledIncomingStep`. This is a base class that provides general concurrency controls for a handler
1. Introduced `ConcurrencyControlledHandlerIncomingStep`. This provides general concurrency controls to any message handler
