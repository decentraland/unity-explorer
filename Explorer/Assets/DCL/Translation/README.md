## Translation Service Architecture Documentation

### 1. Overview

The Translation Service is a system designed to translate in-game chat messages. It supports both automatic translation for conversations and manual, on-demand translation by the user. The architecture is modular, separating concerns into distinct components such as decision-making (policy), data fetching (provider), caching, state management, and message pre-processing.

### 2. Architectural Flow

The service handles two primary use cases: automatic translation of incoming messages and manual translation triggered by the user.

#### 2.1. Automatic Translation Flow (Incoming Message)

1.  **Message Received**: The system calls `ITranslationService.ProcessIncomingMessage()` with the message ID, original text, and conversation ID.
2.  **Policy Check**: The service consults the `IConversationTranslationPolicy` to determine if the message *should* be auto-translated. This policy checks:
    *   If the global translation feature is enabled.
    *   If the user has enabled auto-translation for the specific conversation.
    *   If the message is trivial (e.g., empty, a URL).
3.  **State Management**: If the policy check passes, a new `MessageTranslation` object is created with a `Pending` state and stored in the `ITranslationMemory`.
4.  **Event Fired**: A `TranslationEvents.MessageTranslationRequested` event is published to notify the UI that a translation is in progress.
5.  **Translation Execution**: The internal `TranslateInternalAsync` method is called:
    *   **Cache Check**: It first checks the `ITranslationCache` for a pre-existing translation. If found, the process skips to the final step.
    *   **Processing Check**: The service uses the `RequiresProcessing()` method to analyze the message content. A message requires processing if it contains rich-text tags, emojis, dates, currency, or inline slash commands.
    *   **Translation**:
        *   **With Processing**: If required, the `IMessageProcessor` is used. It tokenizes the message, protects special parts, sends only the translatable text to the `ITranslationProvider` (using batch translation if available), and reassembles the final string.
        *   **Without Processing**: For simple text, the `ITranslationProvider` is called directly.
6.  **Store Result**: The successful translation result is stored in the `ITranslationCache` and the `ITranslationMemory` is updated to a `Success` state with the translated text.
7.  **Final Event**: A `TranslationEvents.MessageTranslated` event is published. If any step fails, the memory state is set to `Failed` and a `MessageTranslationFailed` event is published.

#### 2.2. Manual Translation Flow

1.  **User Action**: The user triggers a manual translation, calling `TranslateMessageCommand` or directly invoking `ITranslationService.TranslateManualAsync()`.
2.  **State Management**: The service checks the `ITranslationMemory`. If a record for this message doesn't exist, it creates one. The state is set to `Pending`.
3.  **Event Fired**: A `TranslationEvents.MessageTranslationRequested` event is published.
4.  **Translation Execution**: The flow proceeds identically to step 5 in the "Automatic Translation Flow".

### 3. Component Breakdown

The service is composed of several key interfaces, each with a specific responsibility.

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

*   **`TokType` Enum**:
    *   `Text`: General text that should be translated.
    *   `Tag`: A rich-text tag (e.g., `<color>`, `</link>`).
    *   `Protected`: Text that must not be translated (e.g., world names, user IDs).
    *   `Emoji`: An emoji grapheme.
    *   `Number`: A protected number, date, or currency amount.
    *   `Command`: A slash command (e.g., `/goto`).

#### 4.2. Tokenization Rules Pipeline

The rules are executed in the following sequence:

1.  **`AngleBracketSegmentationRule`**: Performs the initial segmentation, splitting the string into `Tag` and `Text` tokens based on `<` and `>` characters.
2.  **`LinkProtectionRule`**: Finds `<link=...>` tags and changes the `TokType` of the content between the opening and closing tags to `Protected`. This prevents translation of usernames, coordinates, and URLs.
3.  **`SplitTextTokensOnEmojiRule`**: Scans `Text` tokens and splits out any emoji sequences it finds, creating new tokens with the `Emoji` type.
4.  **`SplitNumericAndDateRule`**: Scans `Text` tokens for patterns matching currency amounts, dates (ISO, slash, dot formats), and times (12h/24h). These are extracted into `Number` type tokens.
5.  **`SplitSlashCommandsRule`**: Scans `Text` tokens for any inline slash commands (e.g., `/help`) and splits them into `Command` type tokens.

After this pipeline runs, the processor extracts only the `Text` tokens, sends them to the `ITranslationProvider` for translation, and then reassembles the complete, translated message from the full list of tokens.

### 5. Data Models

*   **`MessageTranslation`**: A class that represents the full state of a translation job for a single message. It holds the original text, the translated text (once available), the current `TranslationState`, and the detected source/target languages.
*   **`TranslationResult`**: A struct that represents the direct output from a translation provider, containing the translated text and the detected source language.
*   **`TranslationEvents`**: A container for all event structs published by the service, such as `MessageTranslated`, `MessageTranslationFailed`, and `ConversationAutoTranslateToggled`.

### 6. Commands

A series of simple command classes are used to decouple the translation logic from the UI/controller layer.

*   **`TranslateMessageCommand`**: Initiates a manual translation for a specific message.
*   **`RevertToOriginalCommand`**: Reverts a translated message back to its original text.
*   **`ToggleAutoTranslateCommand`**: Toggles the automatic translation setting for a conversation.