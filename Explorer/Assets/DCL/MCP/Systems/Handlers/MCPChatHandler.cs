using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.MCP;
using DCL.Utilities;
using Newtonsoft.Json.Linq;
using System.Reflection;
using UnityEngine;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     MCP хэндлер для отправки и чтения чата. Грязный прототип.
    /// </summary>
    public class MCPChatHandler
    {
        public MCPChatHandler(IChatMessagesBus chatMessagesBus)
        {
            chatMessagesBus.MessageAdded += OnChatMessageAdded;
        }

        private void OnChatMessageAdded(ChatChannel.ChannelId channel, ChatChannel.ChatChannelType type, ChatMessage message)
        {
            // Notify MCP WS subscribers about inbound chat message (unformatted original for AI triggers)
            try { OnInboundMessage(channel, type, message); }
            catch
            { /* ignore */
            }
        }

        private static MCPWebSocketServer ServerInstance;

        public static void AttachServer(MCPWebSocketServer server)
        {
            ServerInstance = server;
        }
        public async UniTask<object> HandleChatSendMessageAsync(JObject parameters)
        {
            // params: { text: string, channel?: "nearby" | "user" | "community", targetId?: string, topic?: string }
            var text = parameters?["text"]?.ToString();

            if (string.IsNullOrWhiteSpace(text))
                return new { success = false, error = "Missing 'text'" };

            var channelIdRaw = parameters?["channelId"]?.ToString();
            string topic = parameters?["topic"]?.ToString() ?? string.Empty;

            if (MCPGlobalLocator.ChatMessagesBus == null)
                return new { success = false, error = "ChatMessagesBus not available" };

            // IChatMessagesBus.Send(ChatChannel channel, string message, string origin, string topic = "")
            object chatMessagesBus = MCPGlobalLocator.ChatMessagesBus;

            ChatChannel chatChannel = ChatChannel.NEARBY_CHANNEL;

            if (string.IsNullOrEmpty(channelIdRaw))
                return new { success = false, error = "Missing 'channelId'" };

            if (ChatChannel.IsCommunityChannelId(channelIdRaw))
                chatChannel = new ChatChannel(ChatChannel.ChatChannelType.COMMUNITY, channelIdRaw);
            else if (string.Equals(channelIdRaw, ChatChannel.ChatChannelType.NEARBY.ToString(), StringComparison.OrdinalIgnoreCase))
                chatChannel = ChatChannel.NEARBY_CHANNEL;
            else
                chatChannel = new ChatChannel(ChatChannel.ChatChannelType.USER, channelIdRaw);

            // В origin укажем MCP
            MethodInfo? sendMethod = chatMessagesBus.GetType().GetMethod("Send");

            if (sendMethod == null)
                return new { success = false, error = "Send method not found on ChatMessagesBus" };

            try
            {
                sendMethod.Invoke(chatMessagesBus, new object[] { chatChannel, text, "MCP", topic ?? string.Empty });

                // Broadcast to WS subscribers about new outgoing message
                if (ServerInstance != null)
                {
                    try
                    {
                        await ServerInstance.BroadcastEventAsync("chat/message", new
                        {
                            direction = "outgoing",
                            channelId = chatChannel.Id.Id,
                            type = chatChannel.ChannelType.ToString(),
                            message = text,
                            topic = topic ?? string.Empty,
                            timestamp = DateTime.UtcNow.ToString("o"),
                        });
                    }
                    catch
                    { /* ignore broadcast errors */
                    }
                }
                return new { success = true };
            }
            catch (Exception e) { return new { success = false, error = e.Message }; }
        }

        public async UniTask<object> HandleChatGetRecentAsync(JObject parameters)
        {
            // params: { channel?: "nearby"|"user"|"community", targetId?: string, limit?: number }
            int limit = Math.Max(1, (int?)parameters?["limit"] ?? 50);
            var channelIdRaw = parameters?["channelId"]?.ToString();

            if (MCPGlobalLocator.ChatHistory == null)
                return new { success = false, error = "ChatHistory not available" };

            var chatHistory = (IChatHistory)MCPGlobalLocator.ChatHistory;

            ChatChannel.ChannelId channelId;
            ChatChannel.ChatChannelType channelType = ChatChannel.ChatChannelType.NEARBY;

            if (string.IsNullOrEmpty(channelIdRaw))
                return new { success = false, error = "Missing 'channelId'" };

            channelId = new ChatChannel.ChannelId(channelIdRaw);

            channelType = ChatChannel.IsCommunityChannelId(channelId) ? ChatChannel.ChatChannelType.COMMUNITY :
                string.Equals(channelIdRaw, ChatChannel.ChatChannelType.NEARBY.ToString(), StringComparison.OrdinalIgnoreCase) ? ChatChannel.ChatChannelType.NEARBY : ChatChannel.ChatChannelType.USER;

            // Пытаемся достать канал и вернуть последние сообщения
            if (!chatHistory.Channels.TryGetValue(channelId, out ChatChannel chatChannel))
            {
                // если канала нет — вернём пустой список
                return new
                {
                    success = true,
                    channel = new { id = channelId.Id, type = channelType.ToString() },
                    messages = Array.Empty<object>(),
                };
            }

            var msgs = chatChannel.Messages.Take(limit)
                                  .Select(m => new
                                   {
                                       message = m.Message,
                                       senderName = m.SenderValidatedName,
                                       senderWalletId = m.SenderWalletId,
                                       senderWalletAddress = m.SenderWalletAddress,
                                       isOwn = m.IsSentByOwnUser,
                                       isSystem = m.IsSystemMessage,
                                       isMention = m.IsMention,
                                       sentTimestamp = m.SentTimestamp?.ToString("o"),
                                       sentTimestampRaw = m.SentTimestampRaw,
                                   })
                                  .ToArray();

            return new
            {
                success = true,
                channel = new { id = chatChannel.Id.Id, type = chatChannel.ChannelType.ToString() },
                messages = msgs,
                totalInChannel = chatChannel.Messages.Count,
            };
        }

        public async UniTask<object> HandleChatListChannelsAsync(JObject parameters)
        {
            // Возвращаем список всех известных каналов из IChatHistory
            if (MCPGlobalLocator.ChatHistory == null)
                return new { success = false, error = "ChatHistory not available" };

            var chatHistory = (IChatHistory)MCPGlobalLocator.ChatHistory;

            var channels = chatHistory.Channels
                                      .Select(kv => new
                                       {
                                           id = kv.Key.Id,
                                           type = kv.Value.ChannelType.ToString(),
                                           totalMessages = kv.Value.Messages.Count,
                                           readMessages = kv.Value.ReadMessages,
                                       })
                                      .ToArray();

            return new { success = true, channels };
        }

        // Hook for inbound messages (from bus) to broadcast to WS
        public static void OnInboundMessage(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType type, ChatMessage message)
        {
            if (ServerInstance == null) return;

            // Avoid system messages spam
            if (message.IsSystemMessage) return;

            try
            {
                ServerInstance.BroadcastEventAsync("chat/message", new
                               {
                                   direction = "incoming",
                                   channelId = channelId.Id,
                                   type = type.ToString(),
                                   message = message.Message,
                                   senderName = message.SenderValidatedName,
                                   senderWalletId = message.SenderWalletId,
                                   senderWalletAddress = message.SenderWalletAddress,
                                   isOwn = message.IsSentByOwnUser,
                                   isMention = message.IsMention,
                                   sentTimestamp = message.SentTimestamp?.ToString("o"),
                                   sentTimestampRaw = message.SentTimestampRaw,
                               })
                              .Forget();
            }
            catch
            { /* ignore */
            }
        }
    }
}
