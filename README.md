# Rebus.SingleAccessSagas

## Background

The background for this repository can be found in an [issue](https://github.com/rebus-org/Rebus/issues/341) and in the [original pull request](https://github.com/rebus-org/Rebus/pull/632) opened against Rebus.

## What is it?

An addon to Rebus that ensures that allows controls over the level of concurrency handlers achieve. It originally started as making sure that only one worker thread takes action for a saga at a given time however it has since been expanded to allow for generalised concurrency controls.

## Why is it needed?

### Sagas

When a saga finishes executing the data associated with it needs to be stored in the saga storage for the next message processed in the saga. But if Thread A and Thread B both perform work concurrently how do you update that data? When I first started toying with this idea Rebus had no resolution for this issue. Since that time it now allows saga handlers to override `ResolveConflict` so that you can attempt to merge the results of the two workers.

This however may not always be achievable. Enter the Single Access Saga.

### Handlers

Concurrency controlled handlers - `IHandleConcurrencyControlledMessages` - allow concurrency controls to be applied, at runtime, to a given message. Whilst a lot of the time concurrnecy controls can be achieved as the saga level there's times when you might have cross-saga-concurrency concerns. Imagine that when a new user signs up to your site you being a `UserProvisionSaga` for that user. It takes care of the usual suspects; email verification, user creation, and charges their credit card for account activation. The single access saga helps ensure only one step of this happens at once. However perhaps your credit card gateway can only handle 2 concurrent requests at a time. If three users sign up at once we might try and perform three card charges. Whilst we could push all card charges into its own saga it's more of an infrastructural concern. Concurrency controlled messages allow us to, at the infrastructure level, say that only two `ChargeCustomerCardCommand` messages get processed at once.

Additionally it because the controls are applied at run time we can inspect the contents of the message and determine different concurrency controls as we handle it. We might let trial customers only have 1 message at a time flow through the system but paid customers might get 10. Or an enterprise customer might be allowed 100. 

## How does it work?

For both handlers and sagas a (series) of locks are acquired before the handler is executed.

### Sagas

Single Access Sagas work by acquiring an exlusive lock for a saga before the saga's handler is executed. If the lock cannot be acquired then the message is deferred. If a single message has multiple saga handlers then all locks must be acquired. If any can not be acquired then the message is deferred.

### Handlers

For concurrency controlled handlers all requisite locks for the message handlers are taken. If they're all acquired then the message is processed as per normal. If anyone lock fails to be acquired the message is deferred for later processing.

## How do I get it?

You can either clone this repository and build the source yourself or grab it via NuGet;

```
Install-Package Rebus.SingleAccessSagas -Pre
```

Note: For now the package is marked as a pre-release package.

## How do I use it?

### Sagas

First in your bootstrap code where you configure your bus you need to enable single access sagas;

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

### Handlers

In your bootstrap code, when configuring the bus, enable concurrency controlled handlers;

```C#
RebusConfigurer config = Configure.With(...);

config.Options(o => o.EnableConcurrencyControlledHandling());
```

Then rather than implementing `IHandleMessages<TMessageType>` implement `IHandleConcurrencyControlledMessages<TMessageType>`. As this extends `IHandleMessages<TMessageType>` you provide your `Handle(TMessageType message)` as per usual. However you will also need to implement a `ConcurrencyControlInfo GetConcurrencyControlInfoForMessage(TMessageType message)` method. From this method you can return a `ConcurrencyControlInfo` class which defines the `LockIdentifier` (effectively yhe scope of the lock), a `MaxConcurrency` (the number of concurrent operations with a matching `LockIdentifier`, and an `OperationCost` (the relative cost of this operation).

#### Examples

##### Single Access for all messages of a type

```C#
public class SingleAccessHandler : IHandleConcurrencyControlledMessages<SingleMessage> {
  public Task Handle(SingleMessage message) {
    Console.WriteLine("Only one of me will ever fire at a time!");
  }
  
  public ConcurrencyControlInfo GetConcurrencyControlInfoForMessage(SingleMessage message) {
    // Called before Handle()
    return new ConcurrencyControlInfo("SingleMessage");
  }
}
```

##### Limit Throughput by Customer Id to 2 messages

```C#
public class LimitedByCustomerHandler : IHandleConcurrencyControlledMessages<MessageFromCustomer> {
  public Task Handle(MessageFromCustomer message) {
    Console.WriteLine("I'll be throttled by customer!!");
  }
  
  public ConcurrencyControlInfo GetConcurrencyControlInfoForMessage(MessageFromCustomer message) {
    // Called before Handle()
    return new ConcurrencyControlInfo(string.Format("Customer::{0}", message.CustomerId), 2);
  }
}
```

##### Different costs per message

```C#
public class CreditCardHandler : IHandleConcurrencyControlledMessages<ChargeCustomerCardMessage>, IHandleConcurrencyControlledMessages<VerifyCustomerCardMessage>,  {
  private const int TotalConcurrencyAllowed = 5

  public Task Handle(ChargeCustomerCardMessage message) {
    Console.WriteLine("Charging a customer card! This is an expensive operation!");
  }
  
  public ConcurrencyControlInfo GetConcurrencyControlInfoForMessage(ChargeCustomerCardMessage message) {
    // Called before Handle()
    // Charging the credit card is a costly operation. So it has a cost of 3 of 5
    return new ConcurrencyControlInfo("CreditCardProcessing", TotalConcurrencyAllowed, 3);
  }
  
  
  public Task Handle(VerifyCustomerCardMessage message) {
    Console.WriteLine("Verifying the customer's card isn't too expensive!");
  }
  
  public ConcurrencyControlInfo GetConcurrencyControlInfoForMessage(VerifyCustomerCardMessage message) {
    // Called before Handle()
    // Verifying the card isn't very costly. So it has a cost of 1 of 5
    return new ConcurrencyControlInfo("CreditCardProcessing", TotalConcurrencyAllowed, 1);
  }
}
```

Two important points here;
  1. The `LockIdentifier` is the same for both the `ChargeCustomerCardMessage` and the `VerifyCustomerCardMessage`. This means they both require the same lock.
  2. The cost of `ChargeCustomerCardMessage` is 3 of 5. That means we can process at most one of these messages. However the `VerifyCustomerCardMessage` only costs 1 of 5. So we coudl process one `ChargeCustomerCardMessage` and 2 `VerifyCustomerCardMessage` messages. However if we were currently processing 3 `VerifyCustomerCardMessage` messages we would be unable to process a `ChargeCustomerCardMessage` message and it would be deferred and retried.

## Why do I have to enable it? Why doesn't it apply everywhere?

Acquiring locks take resources; CPU time, wall clock time, etc. It will lower the throughput of your message queues. So if you don't need a lock for a saga, or concurrency controls for a message, don't use them.

## What sort of lock is used?

If, at the time of calling `EnableSingleAccessSagas` or `EnableConcurrencyControlledHandling`, no implementation of `IHandlerLockProvider`` has been registered then a machine wide `Semaphore` will be used. This will only protect your sagas if all worker threads are running on the same machine. Which is unlikely to be the case in a real world implementation.

## Can I provide my own lock?

Yes! I strongly encourage you to provide your own locking mechanism based on your environment. To do this you need to do three things;
  1. Implement `Rebus.SingleAccessSagas.IHandlerLockProvider`. When `LockFor` is called you should return an implementation of `IHandlerLock` constructed with enough information to acquire the lock relevant to the provided `ConcurrencyControlInfo`. But don't acquire the lock yet!
  1. Implement `Rebus.SingleAccessSagas.IHandlerLock`. It's a simple interface; when `TryAcquire()` is called you should attempt to acquire the lock. Be sure to respect `ConcurrencyControlInfo.MaxConcurrency` and consume as much of that lock as `ConcurrencyControlInfo.OpreationCost` requires. Return `true` if the lock was acquired and `false` if it wasn't. When `Dispose()` is called you should release any resources and if the lock had been acquired then also release the lock so the next handler can use it.
  1. Register your locking mechanism; ```config.Options(opt => opt.Register<IHandlerLockProvider>(res => new MyHandlerLockProvider()));```
  1. If your locking mechanism is based on a lease that requires renewal you can make your `IHandlerLockProvider` implementation wrap your `ISagaLock` in a `Rebus.SingleAccessSagas.AutomaticallyRenewingHandlerLock` which will renew the lock periodically on your behalf.
  
That's it!

## Can I control how messages are retried?

Sort of! As it stands messages for which all locks cannot be acquired will be deferred for later processing. You can control the specifics of the timing behind the retry of messages through providing your own implementation of `IHandlerLockRetryStrategy`. The default is to use a random jitter between 5 and 10s. If, at the time of calling `EnableSingleAccessSagas` or `EnableConcurrencyControlledHandling` no implementation has been provided this default will be used.
