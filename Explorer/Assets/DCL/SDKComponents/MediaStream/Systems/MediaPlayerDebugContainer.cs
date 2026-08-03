using Cysharp.Threading.Tasks;
using DCL.AvProSwitch;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using UUAV;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     The "Media Player" debug tab: traces the media stack from the current scene's
    ///     PB components down to the native UUAV runtime. Passive - data refreshes only on
    ///     the Update button, or every frame while the Auto Update checkbox is on
    ///     (mirrors <c>VoiceChatDebugContainer</c>).
    /// </summary>
    public class MediaPlayerDebugContainer : IDisposable
    {
        private CancellationTokenSource? autoUpdateCts;

        public MediaPlayerDebugContainer(IDebugContainerBuilder debugContainer, MediaPlayerDebugRegistry registry)
        {
            var backendMarker = new ElementBinding<string>(string.Empty);
            var uuavInitialized = new ElementBinding<string>(string.Empty);
            var uuavPlayers = new ElementBinding<ulong>(0);
            var uuavLifecycle = new ElementBinding<string>(string.Empty);
            var uuavAbi = new ElementBinding<string>(string.Empty);
            var uuavPlayersList = new ElementBinding<IReadOnlyList<(string name, string value)>>(Array.Empty<(string name, string value)>());
            var uuavMessages = new ElementBinding<IReadOnlyList<(string name, string value)>>(Array.Empty<(string name, string value)>());

            var sceneLabel = new ElementBinding<string>(string.Empty);
            var sceneCounts = new ElementBinding<string>(string.Empty);
            var playersList = new ElementBinding<IReadOnlyList<(string name, string value)>>(Array.Empty<(string name, string value)>());

            List<string> messagesBuffer = new ();
            List<(string name, string value)> messageRowsBuffer = new ();
            List<UUAVDebug.PlayerInfo> uuavPlayersBuffer = new ();
            List<(string name, string value)> uuavPlayerRowsBuffer = new ();
            List<(string name, string value)> playersBuffer = new ();

            debugContainer.TryAddWidget(IDebugContainerBuilder.Categories.MEDIA_PLAYER)
                         ?.AddCustomMarker("Backend", backendMarker)
                          .AddCustomMarker("UUAV Initialized", uuavInitialized)
                          .AddMarker("UUAV Native Players", uuavPlayers, DebugLongMarkerDef.Unit.NoFormat)
                          .AddList("UUAV Players", uuavPlayersList)
                          .AddCustomMarker("UUAV Lifecycle", uuavLifecycle)
                          .AddCustomMarker("UUAV ABI", uuavAbi)
                          .AddList("UUAV Recent Errors", uuavMessages)
                          .AddCustomMarker("Scene", sceneLabel)
                          .AddCustomMarker("Media In Scene", sceneCounts)
                          .AddList("Media Players", playersList)
                          .AddToggleField("Auto Update", v => AutoUpdateTriggerAsync(v.newValue).Forget(), false)
                          .AddSingleButton("Update", UpdateWidget);

            return;

            void UpdateWidget() =>
                UpdateWidgetAsync(CancellationToken.None).Forget();

            // Requests a collection from the current scene's gather system, gives it two
            // frames to run (it updates in the scene world's own group), then renders.
            async UniTask UpdateWidgetAsync(CancellationToken ct)
            {
                try
                {
                    registry.RequestCollect();

                    bool cancelled = await UniTask.DelayFrame(2, cancellationToken: ct).SuppressCancellationThrow();
                    if (cancelled) return;

                    RenderUuavSection();
                    RenderSceneSection();
                }
                catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.MEDIA_STREAM); }
            }

            void RenderUuavSection()
            {
                backendMarker.Value = MediaPlayerBackendSelection.UseCustomPlayer
                    ? "<color=green>UUAV</color>"
                    : "<color=yellow>AVPro</color>";

                UUAVDebug.Info info = UUAVDebug.Query();

                uuavInitialized.Value = info.NativeLibLoaded ? info.Initialized.ToString() : "library not loaded";
                uuavPlayers.Value = info.PlayersCount;
                uuavAbi.Value = info.AbiVersion;

                uuavLifecycle.Value = info.Lifecycle switch
                                      {
                                          UUAVDebug.Lifecycle.Running => "<color=green>Running</color>",
                                          UUAVDebug.Lifecycle.Recovering => "<color=yellow>Recovering</color>",
                                          UUAVDebug.Lifecycle.Failed => "<color=red>Failed</color>",
                                          UUAVDebug.Lifecycle.ShutDown => "<color=grey>ShutDown</color>",

                                          // expected before init; suspicious once the runtime reports initialized
                                          _ => info.Initialized
                                              ? "<color=grey>Unavailable (stale native binary?)</color>"
                                              : "<color=grey>Unavailable</color>",
                                      };

                UUAVDebug.CopyPlayers(uuavPlayersBuffer);
                uuavPlayerRowsBuffer.Clear();

                foreach (UUAVDebug.PlayerInfo player in uuavPlayersBuffer)
                {
                    string detail = player.PlayerId == 0
                        ? "invalid (native creation failed)"
                        : $"{player.State.ToStringNoAlloc()} {(player.Url.Length > 0 ? player.Url : "none")}";

                    uuavPlayerRowsBuffer.Add(($"id {player.PlayerId}", detail));
                }

                uuavPlayersList.SetAndUpdate(uuavPlayerRowsBuffer);

                UUAVDebug.CopyRecentMessages(messagesBuffer);
                messageRowsBuffer.Clear();

                if (info.DeviceRemoveReason != null)
                    messageRowsBuffer.Add(("device", info.DeviceRemoveReason));

                for (var i = 0; i < messagesBuffer.Count; i++)
                    messageRowsBuffer.Add((i.ToString(), messagesBuffer[i]));

                uuavMessages.SetAndUpdate(messageRowsBuffer);
            }

            void RenderSceneSection()
            {
                // still set: no current-scene gather system consumed the request
                if (registry.CollectRequested || registry.LastCollectedFrame < 0)
                {
                    sceneLabel.Value = "no data - no current scene collector";
                    sceneCounts.Value = string.Empty;
                    playersBuffer.Clear();
                    playersList.SetAndUpdate(playersBuffer);
                    return;
                }

                sceneLabel.Value = registry.SceneLabel;
                sceneCounts.Value = $"video:{registry.VideoPlayerCount} audio:{registry.AudioStreamCount}";

                playersBuffer.Clear();
                playersBuffer.AddRange(registry.Rows);
                playersList.SetAndUpdate(playersBuffer);
            }

            async UniTaskVoid AutoUpdateTriggerAsync(bool enable)
            {
                if (enable)
                {
                    autoUpdateCts = autoUpdateCts.SafeRestart();
                    CancellationToken current = autoUpdateCts.Token;

                    while (current.IsCancellationRequested == false)
                    {
                        await UpdateWidgetAsync(current);

                        bool cancelled = await UniTask.Yield(current).SuppressCancellationThrow();
                        if (cancelled) return;
                    }
                }
                else
                {
                    autoUpdateCts?.Cancel();
                    autoUpdateCts?.Dispose();
                    autoUpdateCts = null;
                }
            }
        }

        public void Dispose()
        {
            autoUpdateCts.SafeCancelAndDispose();
        }
    }
}
