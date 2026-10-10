# Chat System — Detailed Reference

## DynamicWorldContainer Decorator Composition

```csharp
// DynamicWorldContainer.cs — decorator chain composition
IChatMessagesBus coreChatMessageBus =
    new MultiplayerChatMessagesBus(messagePipesHub, chatMessageFactory, ...)
        .WithSelfResend(identityCache, chatMessageFactory)
        .WithIgnoreSymbols()
        .WithCommands(chatCommands, loadingStatus)
        .WithDebugPanel(debugBuilder);
```

---

## CommandsHandleChatMessageBus — Command Dispatch

```csharp
if (message[0] == '/') // User tried running a command
{
    HandleChatCommandAsync(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, message).Forget();
    return;
}
this.origin.Send(channel, message, origin, timestamp); // Not a command — pass through
```

---

## FocusedChatState — State Example

```csharp
public class FocusedChatState : ChatState, IState
{
    public void Enter()
    {
        mediator.SetupForFocusedState();
        inputBlocker.Block();       // Block player movement input
    }

    public override void Exit() => inputBlocker.Unblock();

    public override void OnClickOutside() => stateMachine.Enter<DefaultChatState>();
    public override void OnCloseRequested() => stateMachine.Enter<MinimizedChatState>();
    public override void OnToggleMembers() => stateMachine.Enter<MembersChatState>();
}
```

---

## ChatHistoryEncryptor — Encryption

```csharp
public void SetNewEncryptionKey(string newEncryptionKey)
{
    byte[] hashedKey = shaEncryptor.ComputeHash(Encoding.UTF8.GetBytes(newEncryptionKey));
    cryptoProvider.Key = hashedKey;
    cryptoProvider.IV = hashedKey.AsSpan(0, 16).ToArray();
    cryptoProvider.Mode = CipherMode.ECB;
    cryptoProvider.Padding = PaddingMode.Zeros;
}

public string StringToFileName(string str)
{
    // AES-encrypt -> Base64 -> replace '/' with '_' for filesystem safety
}
```
