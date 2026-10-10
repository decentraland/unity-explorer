# Async Programming

See also: [Architecture Overview](architecture-overview.md#exceptions-free-async-flow) for the `Result` structure and exception-free async flow reference.

## Detached Flows

A detached flow is a `UniTaskVoid` or `UniTask` that is not `awaited` or called with `Forget`.

`Forget()` per se doesn't do anything -- it's sugar for the compiler to suppress the warning "Because this call is not awaited, execution of the current method continues before the call is completed." The developer indicates that they know what they are doing.

When `UniTask` is used, creating a detached flow instantiates a separate delegate attached to a certain moment (`Update` by default) in the player loop and lives in the heap, fully disconnected from the origin where it was created.

### Rules

- Minimize the number of Detached Async Flows
  - There should be a minimal number of topmost functions that launch and forget an async flow. Otherwise, an async flow should be a part of the existing (parent) async flow
- Suppress and report exceptions
  - If the exception is not caught, it will be caught by `Cysharp.Threading.Tasks.ExceptionHolder` and reported from the `destructor`
    - It's a last resort and should not be relied on
    - You will never know when it will be fired
    - It can be invoked on Application.Exit and even lead to a crash
    - It will lose the context
  - `OperationCanceledException` must be ignored -- it indicates a proper cancellation of the flow and should not contaminate the log and Sentry
  - Every other exception should be caught and reported with an appropriate category

  **Improper #1** -- `OperationCanceledException` is not ignored, `Exception` is suppressed but not reported
  ```csharp
  async UniTaskVoid ReconnectRpcClientAsync(CancellationToken ct)
  {
      try
      {
          await socialServicesRPC.DisconnectAsync(ct);
          await socialServicesRPC.EnsureRpcConnectionAsync(int.MaxValue, ct);
      }
      catch (Exception e) when (e is not OperationCanceledException) { }

      socialServiceEventBus.SendTransportReconnectedNotification();
  }
  ```

  **Proper #1** -- `OperationCanceledException` is suppressed, other exceptions are reported
  ```csharp
  async UniTaskVoid ReconnectRpcClientAsync(CancellationToken ct)
  {
      try
      {
          await socialServicesRPC.DisconnectAsync(ct);
          await socialServicesRPC.EnsureRpcConnectionAsync(int.MaxValue, ct);
      }
      catch (OperationCanceledException) { }
      catch (Exception e)
      {
          ReportHub.LogException(e, ReportCategory.ENGINE);
      }

      socialServiceEventBus.SendTransportReconnectedNotification();
  }
  ```

  **Improper #2** -- Exceptions are not handled at all; view may be left in an unknown state
  ```csharp
  async UniTaskVoid AcceptThenCloseAsync(CancellationToken ct)
  {
      await friendsService.AcceptFriendshipAsync(target.Address, ct);

      await ShowOperationConfirmationAsync(
          ViewState.CONFIRMED_ACCEPTED,
          viewInstance!.acceptedConfirmed, target,
          FRIEND_REQUEST_ACCEPTED_FORMAT,
          ct);
  }
  ```

  **Proper #2** -- `SuppressToResultAsync` explicitly states what happens with the result; exceptions are suppressed and reported
  ```csharp
  async UniTaskVoid AcceptThenCloseAsync(CancellationToken ct)
  {
      EnumResult<TaskError> result = await friendsService
          .AcceptFriendshipAsync(target.Address, ct)
          .SuppressToResultAsync(ReportCategory.FRIENDS);

      if (result.Success)
      {
          await ShowOperationConfirmationAsync(
              ViewState.CONFIRMED_ACCEPTED,
              viewInstance!.acceptedConfirmed, target,
              FRIEND_REQUEST_ACCEPTED_FORMAT,
              ct);

          Close();
      }
  }
  ```

  **Improper #3** -- Exceptions not handled
  ```csharp
  async UniTaskVoid CancelFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
  {
      await friendService.CancelFriendshipAsync(inputData.UserId, ct);
      ShowFriendshipInteraction();
  }
  ```

  **Proper #3** -- The result from `SuppressToResultAsync` can be safely ignored if no further action depends on it
  ```csharp
  async UniTaskVoid CancelFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
  {
      await friendService.CancelFriendshipAsync(inputData.UserId, ct)
          .SuppressToResultAsync(ReportCategory.FRIENDS);

      ShowFriendshipInteraction();
  }
  ```

  Or with no continuation at all:
  ```csharp
  async UniTaskVoid CancelFriendRequestThenChangeInteractionStatusAsync(CancellationToken ct)
  {
      await friendService.CancelFriendshipAsync(inputData.UserId, ct)
          .SuppressToResultAsync(ReportCategory.FRIENDS);
      // No continuation needed
  }
  ```

> Further information about the `Result` structure and the exception-free flow can be found in [Architecture Overview](architecture-overview.md#exceptions-free-async-flow)
