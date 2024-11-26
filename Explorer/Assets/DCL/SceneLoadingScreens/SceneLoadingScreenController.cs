using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Utilities.Extensions;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utility;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController : ControllerBase<SceneLoadingScreenView, SceneLoadingScreenController.Params>
    {
        private readonly ISceneTipsProvider sceneTipsProvider;
        private readonly TimeSpan minimumDisplayDuration;
        private readonly AudioMixerVolumesController audioMixerVolumesController;
        private readonly IInputBlock inputBlock;

        private readonly AudioMixerGroup audioMixerGroupController;
        private readonly List<UniTask> fadingTasks = new ();

        private int currentTip;
        private SceneTips tips;
        private CancellationTokenSource? tipsRotationCancellationToken;
        private CancellationTokenSource? tipsFadeCancellationToken;
        private IntVariable? progressLabel;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public SceneLoadingScreenController(ViewFactoryMethod viewFactory,
            ISceneTipsProvider sceneTipsProvider,
            TimeSpan minimumDisplayDuration,
            AudioMixerVolumesController audioMixerVolumesController,
            IInputBlock inputBlock) : base(viewFactory)
        {
            this.sceneTipsProvider = sceneTipsProvider;
            this.minimumDisplayDuration = minimumDisplayDuration;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.inputBlock = inputBlock;
        }

        public override void Dispose()
        {
            base.Dispose();

            tipsRotationCancellationToken?.SafeCancelAndDispose();
            tipsFadeCancellationToken?.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.ShowNextButton.onClick.AddListener(() =>
            {
                ShowTipWithFade(currentTip + 1);
                tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
                RotateTipsOverTimeAsync(tips.Duration, tipsRotationCancellationToken.Token).Forget();
            });

            viewInstance.ShowPreviousButton.onClick.AddListener(() =>
            {
                ShowTipWithFade(currentTip - 1);
                tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
                RotateTipsOverTimeAsync(tips.Duration, tipsRotationCancellationToken.Token).Forget();
            });

            viewInstance.OnBreadcrumbClicked += ShowTipWithFade;

            progressLabel = (IntVariable)viewInstance.ProgressLabel.StringReference["progressValue"];
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            tipsFadeCancellationToken = tipsFadeCancellationToken.SafeRestart();
            BlockUnwantedInputs();
            SetLoadProgress(0);
            viewInstance.ClearTips();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            viewInstance.RootCanvasGroup.alpha = 1f;
            viewInstance.ContentCanvasGroup.alpha = 1f;

            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Chat_Volume);
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            tipsRotationCancellationToken?.SafeCancelAndDispose();
            tipsFadeCancellationToken?.SafeCancelAndDispose();

            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Chat_Volume);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await LoadTipsAsync(ct);

            ShowTip(0);

            tipsRotationCancellationToken = tipsRotationCancellationToken.SafeRestart();
            RotateTipsOverTimeAsync(tips.Duration, tipsRotationCancellationToken.Token).Forget();

            // Waiting should spin no longer than the async load report itself, the life-cycle of the load report is secured by the upper layer
            var combinedToken = inputData.AsyncLoadProcessReport.WaitUntilFinishedAsync().ToCancellationToken(ct);

            await UniTask.WhenAll(WaitUntilWorldIsLoadedAsync(0.8f, combinedToken), WaitTimeThresholdAsync(0.2f, combinedToken))
                         .SuppressCancellationThrow();

            // Fade should be performed if loadingProcess is finished but the "hard" token is still ok
            if (!ct.IsCancellationRequested)
                await FadeOutAsync(ct);
        }

        private async UniTask FadeOutAsync(CancellationToken ct)
        {
            var contentTask = viewInstance.ContentCanvasGroup.DOFade(0f, 0.5f).ToUniTask(cancellationToken: ct);
            var rootTask = viewInstance.RootCanvasGroup.DOFade(0f, 0.7f).ToUniTask(cancellationToken: ct);
            await UniTask.WhenAll(contentTask, rootTask);
            UnblockUnwantedInputs();
        }

        private async UniTask WaitTimeThresholdAsync(float progressProportion, CancellationToken ct)
        {
            float t = 0;

            while (t < 1f && !ct.IsCancellationRequested)
            {
                float time = Time.realtimeSinceStartup;
                await UniTask.NextFrame(cancellationToken: ct);
                float dt = (Time.realtimeSinceStartup - time) / (float)minimumDisplayDuration.TotalSeconds;
                t += dt;
                AddLoadProgress(dt * progressProportion);
            }
        }

        private async UniTask LoadTipsAsync(CancellationToken ct)
        {
            tips = await sceneTipsProvider.GetAsync(ct);

            if (tips.Random)
                tips.Tips.Shuffle();

            foreach (SceneTips.Tip tip in tips.Tips)
                viewInstance.AddTip(tip);
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

            await UpdateProgressBarAsync();
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

            viewInstance.HideAllTips();
            viewInstance.ShowTip(index);

            viewInstance.ChangeBackgroundColor(tips.Tips[index].BackgroundColor);

            currentTip = index;
        }

        private void ShowTipWithFade(int index)
        {
            const float DURATION = 0.3f;

            UniTask FadeOthers(CancellationToken ct)
            {
                fadingTasks.Clear();

                for (var i = 0; i < tips.Tips.Count; i++)
                    fadingTasks.Add(viewInstance.HideTipWithFadeAsync(i, DURATION, ct));

                return UniTask.WhenAll(fadingTasks);
            }

            async UniTaskVoid ShowTipWithFadeAsync(CancellationToken ct)
            {
                if (index < 0)
                    index = tips.Tips.Count - 1;

                index %= tips.Tips.Count;

                currentTip = index;

                await FadeOthers(ct);

                await UniTask.WhenAll
                (
                    viewInstance.ChangeBackgroundColorFadeAsync(tips.Tips[index].BackgroundColor, DURATION, ct),
                    viewInstance.ShowTipWithFadeAsync(index, DURATION, ct)
                );
            }

            ShowTipWithFadeAsync(tipsFadeCancellationToken!.Token).Forget();
        }

        private async UniTaskVoid RotateTipsOverTimeAsync(TimeSpan frequency, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Delay(frequency, cancellationToken: ct);

                    ShowTipWithFade(currentTip + 1);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void BlockUnwantedInputs()
        {
            inputBlock.Disable(InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.PLAYER);
        }

        private void UnblockUnwantedInputs()
        {
            inputBlock.Enable(InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.PLAYER);
        }
    }
}
