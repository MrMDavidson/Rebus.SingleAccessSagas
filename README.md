# Rebus.SingleAccessSagas

## Background

The background for this repository can be found in an [issue](https://github.com/rebus-org/Rebus/issues/341) and in the [original pull request](https://github.com/rebus-org/Rebus/pull/632) opened against Rebus.

## What is it?

An addon to Rebus that ensures that only one worker thread takes action for a saga at a given time.

## Why is it needed?

When a saga finishes executing the data associated with it needs to be stored in the saga storage for the next message processed in the saga. But if Thread A and Thread B both perform work concurrently how do you update that data? When I first started toying with this idea Rebus had no resolution for this issue. Since that time it now allows saga handlers to override `ResolveConflict` so that you can attempt to merge the results of the two workers.

This however may not always be achievable. Enter the Single Access Saga.

## How does it work?

Single Access Sagas work by acquiring a lock for a saga before the saga's handler is executed. If the lock cannot be acquired then the message is deferred. If a single message has multiple saga handlers then all locks must be acquired. If any can not be acquired then the message is deferred.

## How do I use it?

Usage is pretty straight forward. First in your bootstrap code where you configure your bus you need to enable single access sagas;

```C#
RebusConfigurer config = Configure.With(...);

config.Options(o => o.EnableSingleAccessSagas());
```

Then for any sagas you wish to protect simply tag the `Saga` as implementing `Rebus.SingleAccessSagas.ISingleAccessSaga`

```C#
public class MyChattySaga : Saga<MySagaType>,
  IAmInitiatedBy<StartChattySagaCommand>,
  ISingleAccessSaga {
  
  // ....
}
```

## Why do I have to enable it? Why doesn't it apply everywhere?

Acquiring locks take resources; CPU time, wall clock time, etc. It will lower the throughput of your message queues. So if you don't need a lock for a saga, don't use them.

## What sort of lock is used?

If, at the time of calling `EnableSingleAccessSagas`, no implementation of `ISagaLockProvider` has been registered then a machine wide `Semaphore` will be used. This will only protect your sagas if all worker threads are running on the same machine. Which is unlikely to be the case in a real world implementation.

## Can I provide my own lock?

Yes! I strongly encourage you to provide your own locking mechanism based on your environment. To do this you need to do three things;
  1. Implement `Rebus.SingleAccessSagas.ISagaLockProvider`. When `LockFor` is called you should return an implementation of `ISagaLock` constructed with enough information to acquire the lock relevant to `sagaCorrelationId`. But don't acquire the lock yet!
  1. Implement `Rebus.SingleAccessSagas.ISagaLock`. It's a simple interface; when `TryAcquire()` is called you should attempt to acquire the lock. Return `true` if the lock was acquired and `false` if it wasn't. When `Dispose()` is called you should release any resources and if the lock had been acquired then also release the lock so the next handler can use it.
  1. Register your locking mechanism; ```config.Options(opt => opt.Register<ISagaLockProvider>(res => new MySagaLockProvider()));```
  
That's it!
