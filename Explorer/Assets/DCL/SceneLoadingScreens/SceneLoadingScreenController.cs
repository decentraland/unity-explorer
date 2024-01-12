using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.SceneLoadingScreens
{
    public partial class SceneLoadingScreenController : ControllerBase<ScreenLoadingScreenView, SceneLoadingScreenController.Params>
    {
        private readonly ISceneTipsProvider sceneTipsProvider;

        private int currentTip;
        private SceneTips tips;
        private CancellationTokenSource? tipsRotationCancellationToken;

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
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

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
            await UniTask.Delay(1000, cancellationToken: ct);
            viewInstance.ProgressBar.normalizedValue = 0.5f;
            await UniTask.Delay(1000, cancellationToken: ct);
            viewInstance.ProgressBar.normalizedValue = 1f;
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
