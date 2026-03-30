# Chat History Local Storage

In summary, as a user, when you enter Decentraland you should see the chat as it was when you left it last time, except for the nearby conversation which will be empty. The toolbar should show all conversations, and opening one of those should show all the messages received until you disconnected. Chat history is stored locally only, so it will not be visible when logging in on a different machine.

This feature can be disabled using the feature flag "explorer-alfa-chat-history-local-storage". In such case, none of the conversations will be back when re-entering Decentraland.

Note: Usernames of the chat messages are stored, so old chat messages will not reflect the current name of the users.

## Classes involved

* ChatHistoryStorage: The core of the sub-system to store the history, it listens to ChatHistory, is used by the ChatController and uses the encryptor and the serializer internally (see below).
* ChatHistoryEncryptor: It is in charge of encrypting and decrypting streams, file names and channel ids.
* ChatHistorySerializer: It is in charge of assembling and disassembling chat messages so they can be read and written.

## Implementation details

![imagen](https://github.com/user-attachments/assets/b2f9cdf0-2b5a-4f0a-8d63-191d0203347a)

* There is one encrypted JSON file that stores the open conversations, in order. It is created the first time a conversation is open. In case it is corrupted or missing, a new one will be created that includes all the stored chats, even if they were closed in the past.
* There is one encrypted CSV file per conversation, created the first time that conversation receives a message.
* When the chat appears, all conversations that were open will be visible in the toolbar although their messages will not be read from files until the user opens each conversation.
* When a message is sent or received, the file of that conversation is open for writing for a period of time, so it is not opening / closing for every message.
* If a conversation is closed, its history remains stored in a file. When it is re-opened, that file is read.
* The only way to erase the history is by using the Delete history button in the chat panel.
* The chat data is stored in `C:\Users\<your user>\AppData\LocalLow\Decentraland\Explorer\c` (Windows)
     or `~/Library/Application Support/Decentraland/Explorer/c` (macOS).
* The name of each file is encrypted too and transformed to Base64.
* There is one folder per account, so the user should be able to log in with different accounts in the same machine.
* Chat messages are stored after formatted, this means, any link / mention / emoji processing has been already performed and will not be repeated when messages are loaded.
* Enable the CHAT_HISTORY options in the ReportsHandlingSettingsDevelopment to see all the logs.

## Troubleshooting

If the chat history fails to load, you can delete the "c" folder mentioned above. Everything should be re-generated the next time the user enters DCL although the history will be lost.
