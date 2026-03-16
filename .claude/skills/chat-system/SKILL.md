---
name: chat-system
description: "Chat system architecture — MVP pattern, message bus decorator chain, commands, auto-translation, encrypted history, and input state machine. Use when adding chat commands, modifying message flow, working with chat services (history, translation), implementing message decorators, or extending chat UI states."
user-invocable: false
---

# Chat System

## Sources

- `docs/chat.md` — Chat architecture (MVP, state machine, commands, services, event bus, auto-translation)
- `docs/chat-emojis.md` — Emoji atlas creation with TextMesh Pro, noto-emoji font
- `docs/chat-history-local-storage.md` — Local encrypted chat history persistence, feature flag control

---

## Architecture Overview

The chat system uses **MVP + State Machine + EventBus + Commands**:

- **View** (MonoBehaviour): draw-only; forwards UI events
- **Presenter** (POCO): listens to View + EventBus, delegates to Commands/Services, updates View
- **Model**: embodied by Services and data stores (`IChatHistory`)
- **Commands**: single-purpose business logic (e.g., `SendMessageCommand`)
- **Services**: long-lived shared state or I/O boundaries (history, member lists, input blocking)
- **EventBus**: decoupled comms via `ChatEvents` (no tight coupling between Presenters)
- **State Machine**: `ChatStateMachine` controls top-level UI mode; replaces boolean soup
- **Composition root**: `ChatPlugin` wires Views, Presenters, Commands, Services, and State Machine

### Component Map

| Layer | Key Classes |
|-------|------------|
| Composition | `ChatPlugin`, `ChatMainSharedAreaController` |
| Presenters | `ChatPanelPresenter`, `ChatTitlebarPresenter`, `ChatInputPresenter`, `ChatMessageFeedPresenter`, `ChatChannelsPresenter`, `ChatMemberListPresenter` |
| Commands | `SendMessageCommand`, `SelectChannelCommand`, `InitializeChatSystemCommand`, `GetMessageHistoryCommand`, `ResolveInputStateCommand`, ~20 more |
| Services | `CurrentChannelService`, `ChatHistoryService`, `ChatMemberListService`, `ChatInputBlockingService`, `ChatContextMenuService`, `ChatWorldBubbleService` |
| State Machine | `ChatStateMachine` with `Init`, `Default`, `Focused`, `Members`, `Minimized`, `Hidden` states |

### Message Send Flow

1. `ChatInputView` raises `onSubmit("Hello")`
2. `ChatInputPresenter` calls `SendMessageCommand`
3. Command pulls active channel from `CurrentChannelService`, calls `IChatMessagesBus`
4. Bus echoes `MessageAdded` locally (optimistic UI)
5. `ChatHistoryService` persists to `IChatHistory`
6. `ChatMessageFeedPresenter` maps via `CreateMessageViewModelCommand` and updates View

---

## Message Bus Decorator Chain

`IChatMessagesBus` is wrapped in decorators, each adding a concern. The chain is composed in `DynamicWorldContainer`:

```csharp
// DynamicWorldContainer.cs — decorator chain composition
IChatMessagesBus coreChatMessageBus =
    new MultiplayerChatMessagesBus(messagePipesHub, chatMessageFactory, ...)
        .WithSelfResend(identityCache, chatMessageFactory)
        .WithIgnoreSymbols()
        .WithCommands(chatCommands, loadingStatus)
        .WithDebugPanel(debugBuilder);
```

### Decorator Responsibilities

| Decorator | Role |
|-----------|------|
| `MultiplayerChatMessagesBus` | Core transport: sends via LiveKit pipes (Island/Scene/Chat), receives from subscribed pipes, deduplicates, rate-limits, buffers nearby messages |
| `SelfResendChatMessageBus` | On `Send()`, also fires `MessageAdded` for the sender's own message (optimistic local echo) |
| `IgnoreWithSymbolsChatMessageBus` | Filters messages containing forbidden control characters (`\u2410`, `\u2406`, `\u2411`) |
| `CommandsHandleChatMessageBus` | Intercepts `/`-prefixed messages, parses command + args, dispatches to `IChatCommand`, sends system response |

### Interface

```csharp
// IChatMessagesBus.cs
public interface IChatMessagesBus : IDisposable
{
    event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage> MessageAdded;
    void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp);
}
```

Each decorator wraps `origin.Send()` and forwards `origin.MessageAdded` events, adding its own logic before or after.

---

## Chat Command Pattern

### Interface

```csharp
// IChatCommand.cs
public interface IChatCommand
{
    string Command { get; }
    string Description { get; }
    bool DebugOnly => false;
    bool ValidateParameters(string[] parameters) => parameters.Length == 0;
    UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct);
}
```

### Existing Commands

| Command | Class | Debug? |
|---------|-------|--------|
| `/goto <x,y\|random\|crowd\|world>` | `GoToChatCommand` | No |
| `/help` | `HelpChatCommand` | No |
| `/version` | `VersionChatCommand` | No |
| `/gotolocal <x,y>` | `GoToLocalChatCommand` | Yes |
| `/reloadscene` | `ReloadSceneChatCommand` | Yes |
| `/debugpanel` | `DebugPanelChatCommand` | Yes |
| `/showentity <id>` | `ShowEntityChatCommand` | Yes |
| `/logs <category> <level>` | `LogsChatCommand` | Yes |
| `/logmatrix` | `LogMatrixChatCommand` | Yes |
| `/rooms` | `RoomsChatCommand` | Yes |
| `/loadpx <urn>` | `LoadPortableExperienceChatCommand` | Yes |
| `/killpx <urn>` | `KillPortableExperienceChatCommand` | Yes |
| `/appargs` | `AppArgsChatCommand` | Yes |

### Command Dispatch (inside `CommandsHandleChatMessageBus`)

```csharp
if (message[0] == '/') // User tried running a command
{
    HandleChatCommandAsync(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, message).Forget();
    return;
}
this.origin.Send(channel, message, origin, timestamp); // Not a command — pass through
```

Commands are registered in `DynamicWorldContainer` as `IReadOnlyList<IChatCommand>` and looked up by name in a dictionary.

---

## Input State Machine

`ChatStateMachine` manages 6 UI states via `MVCStateMachine<ChatState>`. Each state class contains only logic relevant to that state.

### State Transitions

```
Init --> Default (on view show)
Default --> Focused (click inside / focus requested)
Default --> Minimized (close requested)
Default --> Members (toggle members)
Focused --> Default (click outside)
Focused --> Minimized (close requested)
Focused --> Members (toggle members)
Members --> Default (click outside)
Members --> Focused (close/back/toggle)
Minimized --> Focused (focus requested / minimize toggle)
Hidden <-- (SharedSpaceManager hides chat when other panels open)
Hidden --> Default (SharedSpaceManager restores chat)
```

### State Example

```csharp
// FocusedChatState.cs
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

### Base Class

All states inherit `ChatState` which provides virtual no-op methods: `OnClickOutside`, `OnClickInside`, `OnCloseRequested`, `OnFocusRequested`, `OnMinimizeRequested`, `OnToggleMembers`, `OnPointerEnter`, `OnPointerExit`. States override only the transitions they handle.

---

## Auto-Translation

Translation is orchestrated by `TranslationService` with modular components:

| Component | Role |
|-----------|------|
| `ITranslationService` | Central orchestrator for auto and manual translation |
| `ITranslationProvider` (`DclTranslationProvider`) | External API adapter |
| `IMessageProcessor` (`ChatMessageProcessor`) | Tokenizes complex messages, protects non-translatable parts |
| `IConversationTranslationPolicy` | Decides if auto-translate applies (global flag + per-conversation toggle) |
| `ITranslationCache` (`InMemoryTranslationCache`) | Prevents redundant API calls; capacity configured via `ChatConfig.TranslationCacheCapacity` (default 200) |
| `ITranslationMemory` (`InMemoryTranslationMemory`) | Tracks per-message translation state (`Original`, `Pending`, `Success`, `Failed`); capacity 200 |

### Configuration (ChatConfig ScriptableObject)

```csharp
[field: SerializeField] public int TranslationMaxRetries { get; set; } = 1;
[field: SerializeField] public float TranslationTimeoutSeconds { get; set; } = 10.0f;
public int TranslationMemoryCapacity = 200;
public int TranslationCacheCapacity = 200;
```

### Flow

1. `TranslationService.ProcessIncomingMessage()` checks `IConversationTranslationPolicy`
2. Creates `MessageTranslation` with `Pending` state in `ITranslationMemory`
3. Publishes `TranslationEvents.MessageTranslationRequested`
4. Checks `ITranslationCache` for existing result
5. Runs `RequiresProcessing()` -- if text has TMP tags, emojis, dates, or slash commands, uses `IMessageProcessor` (tokenize, protect, batch-translate); otherwise calls `ITranslationProvider` directly
6. Stores result in cache + memory, publishes `TranslationEvents.MessageTranslated`
7. On failure, sets `Failed` state, publishes `TranslationEvents.MessageTranslationFailed`

Concurrency: per-sender serialization via leader/follower gates, global cap of 10 in-flight translations.

---

## Encrypted History

Feature flag: `explorer-alfa-chat-history-local-storage`. Core classes in `Explorer/Assets/DCL/Chat/History/`.

### Storage Layout

```
{persistentDataPath}/c/
  {Base64(AES(walletAddress))}/           <-- per-account folder
    {Base64(AES("UserConversationSettings"))}   <-- encrypted JSON: open channels + order
    {Base64(AES(channelId))}              <-- encrypted CSV per conversation
```

### Encryption

```csharp
// ChatHistoryEncryptor.cs
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

### Key Behaviors

- **Per-account isolation**: wallet address is the encryption key; folder name is `Base64(AES(address))`
- **Lazy file I/O**: files stay open for 5 seconds after last write, then auto-close
- **Background processing**: message queue processed on a thread pool via `UniTask.RunOnThreadPool`
- **Rehydration**: on first DM message, snapshots session messages, loads history from disk, replays session tail to avoid data loss
- **Nearby excluded**: only `ChatChannelType.USER` conversations are persisted

---

## Cross-References

- **mvc-and-ui-architecture** -- MVC controller pattern, `SharedSpaceManager` panel coordination
- **web-requests** -- `IWebRequestController` used by `DclTranslationProvider` and `GoToChatCommand` for API calls
- **feature-flags-and-configuration** -- `FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE` gates history; `FeaturesRegistry` gates rate limiting and message buffering
