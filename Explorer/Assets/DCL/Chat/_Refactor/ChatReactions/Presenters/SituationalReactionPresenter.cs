using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Chat.Reactions
{
    public sealed class SituationalReactionPresenter : IDisposable
    {
        private readonly SituationalReactionService service;
        private CancellationTokenSource cts = new ();

        public SituationalReactionPresenter(SituationalReactionService service)
        {
            this.service = service;
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
                    service.Tick(Time.unscaledDeltaTime);
                    await UniTask.Yield(ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != Camera.main || Camera.main == null) return;

            service.Draw(cam);
        }
    }
}
