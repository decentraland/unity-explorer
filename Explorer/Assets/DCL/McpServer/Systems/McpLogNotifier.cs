using DCL.McpServer.Core;
using DCL.UI.DebugMenu.LogHistory;
using DCL.UI.DebugMenu.MessageBus;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.McpServer.Systems
{
    /// <summary>
    ///     Feeds the two log streams of <see cref="McpNotificationChannel" /> as notifications/message:
    ///     the "scene" stream carries the SDK7 scene's JavaScript console (the debug-menu log bus), and the
    ///     "client" stream carries the whole Unity player/editor log (<see cref="Application.logMessageReceivedThreaded" />,
    ///     which also covers build and editor output). A client subscribes to one stream at a chosen level;
    ///     each entry is formatted and pushed only while that stream has a subscriber.
    /// </summary>
    public sealed class McpLogNotifier : IDisposable
    {
        private readonly McpNotificationChannel channel;
        private readonly DebugMenuConsoleLogEntryBus sceneLogBus;

        public McpLogNotifier(McpNotificationChannel channel, DebugMenuConsoleLogEntryBus sceneLogBus)
        {
            this.channel = channel;
            this.sceneLogBus = sceneLogBus;

            sceneLogBus.MessageAdded += OnSceneLog;
            Application.logMessageReceivedThreaded += OnClientLog;
        }

        private void OnSceneLog(DebugMenuConsoleLogEntry entry) =>
            Emit(McpLogStreams.SCENE, FromSceneType(entry.Type), entry.Message);

        private void OnClientLog(string condition, string stackTrace, LogType type) =>
            Emit(McpLogStreams.CLIENT, FromUnityType(type), condition);

        private void Emit(string stream, McpLogLevel level, string message)
        {
            if (!channel.HasSubscribers(stream)) return;

            channel.Publish(stream, level, new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/message",
                ["params"] = new JObject
                {
                    ["level"] = level.Wire(),
                    ["logger"] = stream,
                    ["data"] = message,
                },
            });
        }

        private static McpLogLevel FromSceneType(LogMessageType type) =>
            type switch
            {
                LogMessageType.Error => McpLogLevel.Error,
                LogMessageType.Warning => McpLogLevel.Warning,
                _ => McpLogLevel.Info,
            };

        private static McpLogLevel FromUnityType(LogType type) =>
            type switch
            {
                LogType.Exception => McpLogLevel.Critical,
                LogType.Assert => McpLogLevel.Error,
                LogType.Error => McpLogLevel.Error,
                LogType.Warning => McpLogLevel.Warning,
                _ => McpLogLevel.Info,
            };

        public void Dispose()
        {
            sceneLogBus.MessageAdded -= OnSceneLog;
            Application.logMessageReceivedThreaded -= OnClientLog;
        }
    }
}
