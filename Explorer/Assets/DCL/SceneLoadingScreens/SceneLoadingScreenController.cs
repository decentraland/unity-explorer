using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.Pool;
using Utility;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController : ControllerBase<SceneLoadingScreenView, SceneLoadingScreenController.Params>
    {
        private readonly ISceneTipsProvider sceneTipsProvider;

        private int currentTip;
        private SceneTips tips;
        private CancellationTokenSource? tipsRotationCancellationToken;
        private IntVariable progressLabel;

        public SceneLoadingScreenController(ViewFactoryMethod viewFactory,
            ISceneTipsProvider sceneTipsProvider) : base(viewFactory)
        {
            this.sceneTipsProvider = sceneTipsProvider;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public override void Dispose()
        {
            base.Dispose();

            tipsRotationCancellationToken?.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.ShowNextButton.onClick.AddListener(() =>
            {
                ShowTip(currentTip + 1);
                tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
                RotateTipsOverTime(tips.Duration, tipsRotationCancellationToken.Token).Forget();
            });

            viewInstance.ShowPreviousButton.onClick.AddListener(() =>
            {
                ShowTip(currentTip - 1);
                tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
                RotateTipsOverTime(tips.Duration, tipsRotationCancellationToken.Token).Forget();
            });

            progressLabel = (IntVariable)viewInstance.ProgressLabel.StringReference["progressValue"];
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            viewInstance.ProgressBar.normalizedValue = 0f;
            progressLabel.Value = 0;
            viewInstance.ClearTips();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            tipsRotationCancellationToken?.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct)
        {
            return ShowTipsAsync(ct)
                  .ContinueWith(() =>
                   {
                       tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
                       RotateTipsOverTime(tips.Duration, tipsRotationCancellationToken.Token).Forget();
                   })
                  .ContinueWith(() => WaitUntilWorldIsLoadedAsync(ct));
        }

        private async UniTask ShowTipsAsync(CancellationToken ct)
        {
            tips = await sceneTipsProvider.Get(inputData.Coordinate, ct);

            List<SceneTips.Tip> list = ListPool<SceneTips.Tip>.Get();

            if (tips.Random)
                tips.Tips.Shuffle(list);
            else
                list.AddRange(tips.Tips);

            foreach (SceneTips.Tip tip in list)
                viewInstance.AddTip(tip);

            currentTip = 0;
            viewInstance.ShowTip(currentTip);

            ListPool<SceneTips.Tip>.Release(list);
        }

        private async UniTask WaitUntilWorldIsLoadedAsync(CancellationToken ct)
        {
            async UniTaskVoid UpdateProgressBarAsync(CancellationToken ct)
            {
                do
                {
                    try
                    {
                        int totalAssetsLoaded = await inputData.SceneReadinessReport.AssetLoadedCount.WaitAsync(ct);

                        if (inputData.SceneReadinessReport.TotalAssetsToLoad <= 0)
                        {
                            await UniTask.NextFrame(ct);
                            continue;
                        }

                        float progress = Mathf.Clamp01(totalAssetsLoaded / (float)inputData.SceneReadinessReport.TotalAssetsToLoad);
                        viewInstance.ProgressBar.normalizedValue = progress;
                        progressLabel.Value = (int)(progress * 100);
                    }
                    catch (OperationCanceledException) { }
                }
                while (viewInstance.ProgressBar.normalizedValue < 1);
            }

            var progressUpdatingCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(ct);
            UpdateProgressBarAsync(progressUpdatingCancellationToken.Token).Forget();

            try
            {
                await inputData.SceneReadinessReport.CompletionSource.Task;
                progressUpdatingCancellationToken.Cancel();
                ct.ThrowIfCancellationRequested();
                viewInstance.ProgressBar.normalizedValue = 1f;
                progressLabel.Value = 100;
            }
            catch { }
        }

        private void ShowTip(int index)
        {
            if (index < 0)
                index = tips.Tips.Count - 1;

            index %= tips.Tips.Count;

            viewInstance.ShowTip(index);

            currentTip = index;
        }

        private async UniTaskVoid RotateTipsOverTime(TimeSpan frequency, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Delay(frequency, cancellationToken: ct);

                    ShowTip(currentTip + 1);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
