using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat.ChatReactions
{
    public sealed class SituationalReactionPresenter : IDisposable
    {
        private readonly SituationalReactionService service;
        private readonly ChatReactionsDebugConfig? debugConfig;
        private readonly RectTransform? debugButtonRect;
        private CancellationTokenSource cts = new ();

#if UNITY_EDITOR || DEBUG
        private bool prevStreamUI;
        private bool prevStreamLocal;
        private bool prevStreamRemote;
#endif

        public SituationalReactionPresenter(SituationalReactionService service,
            ChatReactionsDebugConfig? debugConfig = null,
            Button? debugButtonRect = null)
        {
            this.service = service;
            this.debugConfig = debugConfig;
            this.debugButtonRect = debugButtonRect.GetComponent<RectTransform>();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            UpdateLoopAsync(cts.Token).Forget();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            service.Dispose();
        }

        private async UniTask UpdateLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    float dt = Time.unscaledDeltaTime;
                    service.Tick(dt);

#if UNITY_EDITOR || DEBUG
                    if (debugConfig != null)
                    {
                        ApplyDebugToggles();
                        debugConfig.UpdateStats(
                            service.UIAliveCount,
                            service.UIPoolCapacity,
                            service.WorldAliveCount,
                            service.WorldPoolCapacity,
                            service.NearbyAvatarCount,
                            service.IsUIStreaming,
                            service.IsWorldStreaming,
                            service.IsDebugNearbyActive);
                    }
#endif

                    await UniTask.Yield(ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }

#if UNITY_EDITOR || DEBUG
        private void ApplyDebugToggles()
        {
            if (debugConfig == null) return;

            if (debugConfig.StreamUILane != prevStreamUI)
            {
                prevStreamUI = debugConfig.StreamUILane;
                if (prevStreamUI)
                    service.BeginDebugUIStream(debugButtonRect);
                else
                    service.EndDebugUIStream();
            }

            if (debugConfig.StreamLocalPlayer != prevStreamLocal)
            {
                prevStreamLocal = debugConfig.StreamLocalPlayer;
                if (prevStreamLocal)
                    service.BeginDebugLocalStream();
                else
                    service.EndDebugLocalStream();
            }

            if (debugConfig.StreamRemotePlayers != prevStreamRemote)
            {
                prevStreamRemote = debugConfig.StreamRemotePlayers;
                if (prevStreamRemote)
                    service.BeginDebugNearby();
                else
                    service.EndDebugNearby();
            }
        }
#endif

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != Camera.main || Camera.main == null) return;

            service.Draw(cam);
        }
    }
}
