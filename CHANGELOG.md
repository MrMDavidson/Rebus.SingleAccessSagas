# Change Log

## Unreleased

1. Added: Add marker interface `IAmInitiatedByConcurrencyControlledMessages` for being initiated by a concurrency controlled message

## 0.1.0-alpha1 to 0.1.0-alpha2

1. Changed: `ConcurrencyControlledHandlingExtentions` is now `ConcurrencyControlledHandlingExtensions`.

## 0.0.1-alpha6 to 0.1.0-alpha1 

1. Added: Introduced `ConcurrencyControlInfo`, This class describes concurrency requirements. `LockIdentifier` is analogous to the `sagaCorrelationId` argument to `ISagaLockProvider::LockFor`. It defines the "scope" of the concurrency requirement and should be treated as an opaque value. `MaxConcurrency` specifies how many concurrent operations for the given `LockIdentifier` are allowed. And `OperationCost` defines the cost of the current operation. If these values are equal (eg. 1 and 1) only a single operation can occur at once. If `MaxConcurrency` is greater than `OperationCost` than multiple operations may occur at once.
1. Changed/Removed: `ISagaLock` has been renamed to `IHandlerLock`. Interface remains the same
1. Changed/Removed: `ISagaLockProvider` has been replaced by `IHandlerLockProvider`. The new API takes a `ConcurrencyControlInfo`.
1. Changed/Removed: `SemaphoreSagaLockProvider` has been renamed to `SemaphoreHandlerLockProvider`
1. Changed/Removed: `SemaphoreSagaLock` has been renamed to `SemaphoreHandlerLock`
1. Changed/Removed: `AutomaticallyRenewingSagaLock` has been renamed to `AutomaticallyRenewingHandlerLock`
1. Changed/Removed: `ISagaLockRetryStrategy` has been renamed to `IHandlerLockRetryStrategy`. Interface remains the same.
1. Changed/Removed: `RandomJitterSagaLockRetryStrategy` has been renamed to `RandomJitterHandlerLockRetryStrategy`
1. Added: Introduced `BaseConcurrencyControlledIncomingStep`. This is a base class that provides general concurrency controls for a handler
1. Added: Introduced `ConcurrencyControlledHandlerIncomingStep`. This provides general concurrency controls to any message handler
