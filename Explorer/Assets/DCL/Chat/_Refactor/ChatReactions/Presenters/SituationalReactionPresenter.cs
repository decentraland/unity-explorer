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
        private readonly ISituationalReactionSimulation service;
        private readonly ChatReactionsConfig config;
        private readonly ChatReactionDebugState debugState;
        private readonly SituationalReactionDebugController? debugController;
        private readonly RectTransform? debugButtonRect;
        private readonly CancellationTokenSource cts = new ();

        private bool prevStreamUI;
        private bool prevStreamLocal;
        private bool prevStreamRemote;

        public SituationalReactionPresenter(ISituationalReactionSimulation service,
            ChatReactionsConfig config,
            ChatReactionDebugState debugState,
            SituationalReactionDebugController? debugController = null,
            Button? debugButton = null)
        {
            this.service = service;
            this.config = config;
            this.debugState = debugState;
            this.debugController = debugController;
            this.debugButtonRect = debugButton != null ? debugButton.GetComponent<RectTransform>() : null;

            if (this.debugButtonRect != null)
                service.SetDefaultUISpawnRect(this.debugButtonRect);

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            UpdateLoopAsync(cts.Token).Forget();
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            cts.SafeCancelAndDispose();
            debugController?.Dispose();
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

                    if (config.DebugEnabled)
                    {
                        ApplyDebugToggles();
                        Profiler.BeginSample("ChatReactions.DebugStats");
                        if (debugController != null)
                            debugState.UpdateStats(debugController.GetStats(debugState.IsDebugNearbyActive));
                        Profiler.EndSample();
                    }

                    await UniTask.Yield(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
            }
        }

        private void ApplyDebugToggles()
        {
            if (debugController == null) return;

            if (config.StreamUILane != prevStreamUI)
            {
                prevStreamUI = config.StreamUILane;
                if (prevStreamUI)
                    debugController.BeginDebugUIStream(debugButtonRect);
                else
                    debugController.EndDebugUIStream();
            }

            if (config.StreamLocalPlayer != prevStreamLocal)
            {
                prevStreamLocal = config.StreamLocalPlayer;
                if (prevStreamLocal)
                    debugController.BeginDebugLocalStream();
                else
                    debugController.EndDebugLocalStream();
            }

            if (config.StreamRemotePlayers != prevStreamRemote)
            {
                prevStreamRemote = config.StreamRemotePlayers;
                if (prevStreamRemote)
                {
                    debugController.BeginDebugNearby();
                    debugState.IsDebugNearbyActive = true;
                }
                else
                {
                    debugController.EndDebugNearby();
                    debugState.IsDebugNearbyActive = false;
                }
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            Profiler.BeginSample("ChatReactions.CameraCheck");
            Camera mainCam = Camera.main;
            if (cam != mainCam || mainCam == null)
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
