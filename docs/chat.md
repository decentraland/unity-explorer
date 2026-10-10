# Chat Architecture Overview

> See also: [Chat Emojis](chat-emojis.md) | [Chat History Storage](chat-history-local-storage.md)

## 1. Purpose and Scope

The chat system is designed as a modular, event-driven UI feature. Its design prioritizes a clear separation of concerns, testability, and scalability. This document outlines the architecture, core components, and development guidelines for contributing to the chat system.

The core architectural goals are:

- **Separation of Concerns:** Clearly divide UI rendering (Views), UI logic (Presenters), business logic (Commands), and data management (Services).
- **Scalability & Maintainability:** Make it easy and safe to add new features or modify existing ones without causing ripple effects.
- **Clarity:** Provide a codebase that is easy for developers to understand and contribute to.
- **Testability:** Enable unit testing for business logic, reducing reliance on manual testing.

## 2. High-Level Architecture

**MVP**:

- **View** (MonoBehaviour): draw-only; forwards UI events.
- **Presenter** (POCO): listens to View + EventBus, delegates to Commands/Services, updates View.
- **Model**: embodied by Services and data stores (e.g., `IChatHistory`).

**Commands**: single-purpose business logic (e.g., `SendMessageCommand`). Presenters call commands, commands use services.

**Services**: long-lived, shared state or I/O boundaries (history, member lists, input blocking).

**EventBus**: decoupled comms via `ChatEvents` (no tight coupling between Presenters).

**State Machine**: `ChatStateMachine` controls top-level UI mode; replaces boolean soup.

**Composition root**: `ChatMainController` wires Views, Presenters, Commands, Services, and State Machine.

## 3. State Machine - The UI Brain

The overall UI behavior is controlled by a **State Machine** (`ChatStateMachine`). This machine manages the primary states of the chat window (e.g., `Focused`, `Minimized`, `MembersList`). It replaces complex boolean flags with explicit, predictable states, making the UI flow robust and easy to follow.

- **Role**: To manage high-level UI states like `Focused`, `Minimized`, `Default`, etc., replacing complex boolean flag logic.
- **Location**: `Scripts/Chat/ChatStates`
- **Key Principle**: Each state class (`FocusedChatState.cs`, etc.) contains logic *only relevant to that state*. For example, `FocusedChatState` is responsible for blocking player input, while `DefaultChatState` is not. This makes UI behavior explicit and predictable.

### States Breakdown

**Init State**

- Internal-only initialization state.
- Not visible to the user.
- Transitions immediately into either Default or Focused state.

**Default State** (Unfocused State)

- The idle state when the user hasn't interacted with the chat.
- What is visible:
  - Input box visible (unfocused)
- What happens:
  - Hover over chat -> background, title bar, and panels fade in
  - Pointer exits chat -> background and panels fade out
  - Click inside chat -> transitions to Focused
  - Click minimize/close -> transitions to Minimized
  - Click member count (Nearby/Community channels) -> transitions to Members

**Focused State**

- Active chat state when the user is typing or interacting.
- What is visible:
  - Title bar visible
  - Channel list visible
  - Input box focused
  - Background fully visible
- What happens:
  - Click outside chat -> transitions to Default
  - Click minimize/close -> transitions to Minimized
  - Click member count -> transitions to Members

**Members State**

- Displays the list of users in Nearby or Community channels.
- What is visible:
  - Only title bar and member list are visible
  - Input box, messages, and channel list are hidden
- What happens:
  - Click outside chat -> transitions to Default
  - Click close/back -> transitions to Focused

**Minimized State**

- Collapsed state showing only the unfocused input box.
- What is visible:
  - Only input box is visible (unfocused)
  - All other UI elements hidden
- What happens:
  - Click input box -> transitions to Focused
  - Click minimize again -> transitions to Focused

**Hidden State**

- Fully invisible chat state used when another panel is open (e.g. Friends).
- What is visible:
  - Nothing visible
- Controlled by external UI panels, not user-interactive.

**Chat Button Behavior**

- If chat is Focused or Default -> clicking chat button -> transitions to Minimized
- If chat is Minimized -> clicking chat button -> transitions to Focused

## 4. Component Inventory

**Presenters/Views**

- `ChatMainController` / `ChatMainView` — composition root + lifecycle (`SharedSpaceManager`).
- `ChatTitlebarPresenter` / `ChatTitlebarView2`
- `ChatChannelsPresenter` / `ChatChannelsView`
- `ChatMessageFeedPresenter` / `ChatMessageFeedView`
- `ChatInputPresenter` / `ChatInputView`
- `ChatMemberListPresenter` / `ChannelMemberFeedView`

**Commands** (selected; keep single-responsibility)

- `InitializeChatSystemCommand`, `RestartChatServicesCommand`, `ResetChatCommand`
- `SendMessageCommand`, `MarkMessagesAsReadCommand`
- `SelectChannelCommand`, `OpenConversationCommand`, `CloseChannelCommand`
- `GetMessageHistoryCommand`, `DeleteChatHistoryCommand`
- `CreateMessageViewModelCommand`, `CreateChannelViewModelCommand`
- `GetTitlebarViewModelCommand`, `GetCommunityThumbnailCommand`
- `GetChannelMembersCommand`, `GetParticipantProfilesCommand`, `GetUserChatStatusCommand`
- `ResolveInputStateCommand`

**Services**

- `CurrentChannelService`
- `ChatHistoryService`
- `ChatMemberListService`
- `ChatInputBlockingService`
- `ChatContextMenuService`
- `ChatWorldBubbleService`
- `ICurrentChannelUserStateService` (implemented by `CommunityUserStateService`, `NearbyUserStateService`, `PrivateConversationUserStateService`)

**EventBus & Events** (`Scripts/Chat/ChatEvents.cs`)

- Channel lifecycle: `InitialChannelsLoadedEvent`, `ChannelAddedEvent`, `ChannelUpdatedEvent`, `ChannelLeftEvent`, `ChannelSelectedEvent`
- History: `ChatHistoryClearedEvent`
- UI intents: `FocusRequestedEvent`, `CloseChatEvent`, `ToggleMembersEvent`
- Presence: `UserStatusUpdatedEvent`, `ChannelUsersStatusUpdated`
- System: `ChatResetEvent`, `CurrentChannelStateUpdatedEvent`

## 5. Data and Event Flow (Send Message)

1. **View**: `ChatInputView` raises `onSubmit("Hello")`.
2. **Presenter**: `ChatInputPresenter` receives and calls `SendMessageCommand`.
3. **Command**: pulls `CurrentChannelService` -> active channel; calls `IChatMessagesBus`.
4. **IChatMessagesBus**: echoes `MessageAdded` locally (optimistic UI).
5. **ChatHistoryService**: listens -> persists to `IChatHistory`.
6. **IChatHistory**: raises `MessageAdded`.
7. **Presenter**: `ChatMessageFeedPresenter` maps via `CreateMessageViewModelCommand` -> updates View.
8. UI shows the message.

## 6. Composition and Lifecycle

- `ChatMainController` creates all Presenters/Commands/Services/StateMachine in `OnViewInstantiated`.
- Manual DI via constructors (no `GetComponent` in Presenters).
- Subscribes/unsubscribes to EventBus and disposes resources properly.
- `SharedSpaceManager` controls visibility; Hidden state used when other panels are foregrounded.

## 7. Practices

**Do**

- Keep Views dumb (fields + public events).
- Keep Presenters thin (orchestrate; delegate to Commands/Services).
- Put business logic into Commands/Services.
- Use EventBus for cross-component comms.
- Add a new UI state instead of piling flags.

**Don't**

- View <-> Service direct calls (always via Presenter).
- "Fat Presenters" (extract a Command).
- `GetComponent` in Presenters (constructor DI only).
- Hidden coupling through static singletons (prefer injected Services).

## 8. Guardrails and Consistency

- Naming: `*Presenter`, `*View`, `*Command`, `*Service`, `*ChatState`.
- File locations: match the sections above (e.g., `Scripts/Chat/ChatServices`).
- Async: prefer UniTask; avoid blocking on main thread; UI updates happen on Unity thread.
- Disposal: Presenters/Services implement `IDisposable` if they subscribe to events.
- Testing: unit-test commands/services; Presenter tests with mocked Views/Bus/Services.

## 9. Known Extension Points

- Add more Commands for new use-cases (keep inputs/outputs minimal).
- Add States for new modes (e.g., voice overlay) without disturbing Presenters.
- Extend UserState via `ICurrentChannelUserStateService` implementations.
- Compose new VM commands for view-model mapping (keeps Presenters slim).

## Chat Auto Translation

### 1. Overview

The Translation Service is a system designed to translate in-game chat messages. It supports both automatic translation for conversations and manual, on-demand translation by the user. The architecture is modular, separating concerns into distinct components such as decision-making (policy), data fetching (provider), caching, state management, and message pre-processing.

### 2. Architectural Flow

The service handles two primary use cases: automatic translation of incoming messages and manual translation triggered by the user.

#### 2.1. Automatic Translation Flow (Incoming Message)

1. **Message Received**: The system calls `ITranslationService.ProcessIncomingMessage()` with the message ID, original text, and conversation ID.
2. **Policy Check**: The service consults the `IConversationTranslationPolicy` to determine if the message *should* be auto-translated. This policy checks:
   - If the global translation feature is enabled.
   - If the user has enabled auto-translation for the specific conversation.
   - If the message is trivial (e.g., empty, a URL).
3. **State Management**: If the policy check passes, a new `MessageTranslation` object is created with a `Pending` state and stored in the `ITranslationMemory`.
4. **Event Fired**: A `TranslationEvents.MessageTranslationRequested` event is published to notify the UI that a translation is in progress.
5. **Translation Execution**: The internal `TranslateInternalAsync` method is called:
   - **Cache Check**: It first checks the `ITranslationCache` for a pre-existing translation. If found, the process skips to the final step.
   - **Processing Check**: The service uses the `RequiresProcessing()` method to analyze the message content. A message requires processing if it contains rich-text tags, emojis, dates, currency, or inline slash commands.
   - **Translation**:
     - **With Processing**: If required, the `IMessageProcessor` is used. It tokenizes the message, protects special parts, sends only the translatable text to the `ITranslationProvider` (using batch translation if available), and reassembles the final string.
     - **Without Processing**: For simple text, the `ITranslationProvider` is called directly.
6. **Store Result**: The successful translation result is stored in the `ITranslationCache` and the `ITranslationMemory` is updated to a `Success` state with the translated text.
7. **Final Event**: A `TranslationEvents.MessageTranslated` event is published. If any step fails, the memory state is set to `Failed` and a `MessageTranslationFailed` event is published.

#### 2.2. Manual Translation Flow

1. **User Action**: The user triggers a manual translation, calling `TranslateMessageCommand` or directly invoking `ITranslationService.TranslateManualAsync()`.
2. **State Management**: The service checks the `ITranslationMemory`. If a record for this message doesn't exist, it creates one. The state is set to `Pending`.
3. **Event Fired**: A `TranslationEvents.MessageTranslationRequested` event is published.
4. **Translation Execution**: The flow proceeds identically to step 5 in the "Automatic Translation Flow".

### 3. Component Breakdown

| Component | Role & Responsibilities |
| :--- | :--- |
| **`ITranslationService`** | The central orchestrator. It coordinates all other components to process translation requests but contains no complex business logic itself. |
| **`ITranslationProvider`** | The low-level adapter that communicates with the external translation API. It is responsible for making network calls and parsing responses. The `DclTranslationProvider` is the primary implementation. |
| **`IBatchTranslationProvider`** | An extension of `ITranslationProvider` that supports sending multiple text segments in a single API call for improved performance and reduced network overhead. |
| **`IMessageProcessor`** | Responsible for preparing complex messages for translation. It runs the text through a pipeline of tokenization rules to segment and protect non-translatable parts. |
| **`IConversationTranslationPolicy`** | Makes the initial "yes/no" decision for auto-translation. It encapsulates all business rules, such as global settings and per-conversation preferences. |
| **`ITranslationCache`** | A short-term storage to prevent redundant API calls for the same message. The `InMemoryTranslationCache` provides a simple, session-based implementation. |
| **`ITranslationMemory`** | Tracks the lifecycle and state (`Original`, `Pending`, `Success`, `Failed`) of a translation request for a given message. This allows the UI to react to ongoing translation jobs. |
| **`ITranslationSettings`** | A read-only source for all configuration data, such as the user's preferred language, feature flags, and auto-translate toggles for each conversation. |
| **`IEventBus`** | Used to publish events about the translation lifecycle, allowing other parts of the application (like the UI) to subscribe and react without being tightly coupled to the service. |

### 4. Message Processing Pipeline (Deep Dive)

The `ChatMessageProcessor` is the heart of the system's text handling. It processes messages by passing them through a series of `ITokenizationRule` implementations in a specific order. This pipeline ensures that complex formatting is preserved correctly.

#### 4.1. Tokenization

The processor converts a raw string into a list of `Tok` (tokens). Each token has a `TokType` that defines its category.

- **`TokType` Enum**:
  - `Text`: General text that should be translated.
  - `Tag`: A rich-text tag (e.g., `<color>`, `</link>`).
  - `Protected`: Text that must not be translated (e.g., world names, user IDs).
  - `Emoji`: An emoji grapheme.
  - `Number`: A protected number, date, or currency amount.
  - `Command`: A slash command (e.g., `/goto`).

#### 4.2. Tokenization Rules Pipeline

The rules are executed in the following sequence:

1. **`AngleBracketSegmentationRule`**: Performs the initial segmentation, splitting the string into `Tag` and `Text` tokens based on `<` and `>` characters.
2. **`LinkProtectionRule`**: Finds `<link=...>` tags and changes the `TokType` of the content between the opening and closing tags to `Protected`. This prevents translation of usernames, coordinates, and URLs.
3. **`SplitTextTokensOnEmojiRule`**: Scans `Text` tokens and splits out any emoji sequences it finds, creating new tokens with the `Emoji` type.
4. **`SplitNumericAndDateRule`**: Scans `Text` tokens for patterns matching currency amounts, dates (ISO, slash, dot formats), and times (12h/24h). These are extracted into `Number` type tokens.
5. **`SplitSlashCommandsRule`**: Scans `Text` tokens for any inline slash commands (e.g., `/help`) and splits them into `Command` type tokens.

After this pipeline runs, the processor extracts only the `Text` tokens, sends them to the `ITranslationProvider` for translation, and then reassembles the complete, translated message from the full list of tokens.

### 5. Data Models

- **`MessageTranslation`**: A class that represents the full state of a translation job for a single message. It holds the original text, the translated text (once available), the current `TranslationState`, and the detected source/target languages.
- **`TranslationResult`**: A struct that represents the direct output from a translation provider, containing the translated text and the detected source language.
- **`TranslationEvents`**: A container for all event structs published by the service, such as `MessageTranslated`, `MessageTranslationFailed`, and `ConversationAutoTranslateToggled`.

### 6. Commands

A series of simple command classes are used to decouple the translation logic from the UI/controller layer.

- **`TranslateMessageCommand`**: Initiates a manual translation for a specific message.
- **`RevertToOriginalCommand`**: Reverts a translated message back to its original text.
- **`ToggleAutoTranslateCommand`**: Toggles the automatic translation setting for a conversation.
