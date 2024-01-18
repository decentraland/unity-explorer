using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
        private readonly TimeSpan minimumDisplayDuration;

        private int currentTip;
        private SceneTips tips;
        private CancellationTokenSource? tipsRotationCancellationToken;
        private IntVariable? progressLabel;

        public SceneLoadingScreenController(ViewFactoryMethod viewFactory,
            ISceneTipsProvider sceneTipsProvider,
            TimeSpan minimumDisplayDuration) : base(viewFactory)
        {
            this.sceneTipsProvider = sceneTipsProvider;
            this.minimumDisplayDuration = minimumDisplayDuration;
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
                RotateTipsOverTimeAsync(tips.Duration, tipsRotationCancellationToken.Token).Forget();
            });

            viewInstance.ShowPreviousButton.onClick.AddListener(() =>
            {
                ShowTip(currentTip - 1);
                tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
                RotateTipsOverTimeAsync(tips.Duration, tipsRotationCancellationToken.Token).Forget();
            });

            viewInstance.OnBreadcrumbClicked += ShowTip;

            progressLabel = (IntVariable)viewInstance.ProgressLabel.StringReference["progressValue"];
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            SetLoadProgress(0);
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
                       RotateTipsOverTimeAsync(tips.Duration, tipsRotationCancellationToken.Token).Forget();
                   })
                  .ContinueWith(() => UniTask.WhenAll(WaitUntilWorldIsLoadedAsync(0.6f, ct), WaitTimeThresholdAsync(0.4f, ct)));
        }

        private async UniTask WaitTimeThresholdAsync(float progressProportion, CancellationToken ct)
        {
            float t = 0;

            while (t < 1f && !ct.IsCancellationRequested)
            {
                float now = Time.realtimeSinceStartup;
                await UniTask.NextFrame(ct);
                float dt = Time.realtimeSinceStartup - now;
                t += dt / (float)minimumDisplayDuration.TotalSeconds;
                AddLoadProgress(dt * progressProportion);
            }
        }

        private async UniTask ShowTipsAsync(CancellationToken ct)
        {
            tips = await sceneTipsProvider.GetAsync(ct);

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

        private async UniTask WaitUntilWorldIsLoadedAsync(float progressProportion, CancellationToken ct)
        {
            async UniTask UpdateProgressBarAsync()
            {
                var prevProgress = 0f;

                await foreach (float progress in inputData.AsyncLoadProcessReport.ProgressCounter.WithCancellation(ct))
                {
                    float delta = progress - prevProgress;
                    prevProgress = progress;
                    await UniTask.SwitchToMainThread(ct);
                    AddLoadProgress(delta * progressProportion);
                }
            }

            try
            {
                await UniTask.WhenAny(inputData.AsyncLoadProcessReport.CompletionSource.Task, UpdateProgressBarAsync());
                ct.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.SCENE_LOADING)); }
        }

        private void SetLoadProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            viewInstance.ProgressBar.normalizedValue = progress;
            progressLabel!.Value = (int)(progress * 100);
        }

        private void AddLoadProgress(float progress) =>
            SetLoadProgress(viewInstance.ProgressBar.normalizedValue + progress);

        private void ShowTip(int index)
        {
            if (index < 0)
                index = tips.Tips.Count - 1;

            index %= tips.Tips.Count;

            viewInstance.ShowTip(index);

            currentTip = index;
        }

        private async UniTaskVoid RotateTipsOverTimeAsync(TimeSpan frequency, CancellationToken ct)
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
