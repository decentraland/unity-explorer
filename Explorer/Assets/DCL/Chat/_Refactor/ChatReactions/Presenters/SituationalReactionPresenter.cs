using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat.ChatReactions
{
    public sealed class SituationalReactionPresenter : IDisposable
    {
        private readonly SituationalReactionService service;
        private readonly ChatReactionsConfig config;
        private readonly RectTransform? debugButtonRect;
        private readonly CancellationTokenSource cts = new ();

#if UNITY_EDITOR || DEBUG
        private bool prevStreamUI;
        private bool prevStreamLocal;
        private bool prevStreamRemote;
#endif

        public SituationalReactionPresenter(SituationalReactionService service,
            ChatReactionsConfig config,
            Button? debugButtonRect = null)
        {
            this.service = service;
            this.config = config;
            this.debugButtonRect = debugButtonRect != null ? debugButtonRect.GetComponent<RectTransform>() : null;

            if (this.debugButtonRect != null)
                service.SetDefaultUISpawnRect(this.debugButtonRect);

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            UpdateLoopAsync(cts.Token).Forget();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private async UniTask UpdateLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    float dt = Time.unscaledDeltaTime;

                    Profiler.BeginSample("ChatReactions.Tick");
                    service.Tick(dt);
                    Profiler.EndSample();

#if UNITY_EDITOR || DEBUG
                    if (config.DebugEnabled)
                    {
                        ApplyDebugToggles();
                        Profiler.BeginSample("ChatReactions.DebugStats");
                        config.UpdateStats(
                            service.UIAliveCount,
                            service.UIPoolCapacity,
                            service.WorldAliveCount,
                            service.WorldPoolCapacity,
                            service.NearbyAvatarCount,
                            service.IsUIStreaming,
                            service.IsWorldStreaming,
                            service.IsDebugNearbyActive);
                        Profiler.EndSample();
                    }
#endif

                    await UniTask.Yield(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
            }
        }

#if UNITY_EDITOR || DEBUG
        private void ApplyDebugToggles()
        {
            if (config.StreamUILane != prevStreamUI)
            {
                prevStreamUI = config.StreamUILane;
                if (prevStreamUI)
                    service.BeginDebugUIStream(debugButtonRect);
                else
                    service.EndDebugUIStream();
            }

            if (config.StreamLocalPlayer != prevStreamLocal)
            {
                prevStreamLocal = config.StreamLocalPlayer;
                if (prevStreamLocal)
                    service.BeginDebugLocalStream();
                else
                    service.EndDebugLocalStream();
            }

            if (config.StreamRemotePlayers != prevStreamRemote)
            {
                prevStreamRemote = config.StreamRemotePlayers;
                if (prevStreamRemote)
                    service.BeginDebugNearby();
                else
                    service.EndDebugNearby();
            }
        }
#endif

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            Profiler.BeginSample("ChatReactions.CameraMain");
            if (cam != Camera.main || Camera.main == null)
            {
                Profiler.EndSample();
                return;
            }
            Profiler.EndSample();

            Profiler.BeginSample("ChatReactions.Draw");
            service.Draw(cam);
            Profiler.EndSample();
        }
    }
}
