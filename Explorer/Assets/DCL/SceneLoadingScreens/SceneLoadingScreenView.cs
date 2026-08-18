using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DG.Tweening;
using MVC;
using RichTypes;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens
{
    public class SceneLoadingScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public CanvasGroup RootCanvasGroup { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup ContentCanvasGroup { get; private set; } = null!;

        [field: SerializeField]
        public Slider ProgressBar { get; private set; } = null!;

        [field: SerializeField]
        public LocalizeStringEvent ProgressLabel { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text LoadingPercentageText { get; private set; } = null!;

        [field: SerializeField]
        public Button ShowNextButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ShowPreviousButton { get; private set; } = null!;

        /// <summary>Nullable so prefabs predating the button keep working.</summary>
        [field: SerializeField]
        public Button? BugReportButton { get; private set; } = null!;

        [SerializeField]
        private Transform tipsParent = null!;

        [SerializeField]
        private TipView tipViewPrefab = null!;

        [SerializeField]
        private LoadingTipCatalogSO tipCatalog = null!;

        [field: SerializeField]
        public Image Background { get; private set; } = null!;

        [SerializeField]
        private TipBreadcrumb breadcrumbPrefab = null!;

        [SerializeField]
        private Transform breadcrumbParent = null!;

        public event Action<int>? OnBreadcrumbClicked;

        private readonly List<TipView> tips = new ();
        private readonly List<TipBreadcrumb> tipsBreadcrumbs = new ();

#if UNITY_EDITOR
        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Awake()
        {
            // Copy material in editor so we don't get asset changes
            Background.material = new Material(Background.material);
        }
#endif

        public void ClearTips()
        {
            foreach (TipView tip in tips)
                Destroy(tip.gameObject);

            foreach (TipBreadcrumb? breadcrumb in tipsBreadcrumbs)
                Destroy(breadcrumb.gameObject);

            tips.Clear();
            tipsBreadcrumbs.Clear();
        }

        public void AddTip(SceneTips.LoadedTip tip)
        {
            TipView view = Instantiate(tipCatalog.TryGet(tip.Key, out TipView? preConfiguredPrefab)
                ? preConfiguredPrefab! : tipViewPrefab, tipsParent);

            view.Set(tip);

            TipBreadcrumb breadcrumb = Instantiate(breadcrumbPrefab, breadcrumbParent);
            int breadcrumbIndex = tipsBreadcrumbs.Count;
            breadcrumb.Button.onClick.AddListener(() => OnBreadcrumbClicked?.Invoke(breadcrumbIndex));

            tips.Add(view);
            tipsBreadcrumbs.Add(breadcrumb);
        }

        public void ShowTip(int index)
        {
            foreach (TipBreadcrumb? breadcrumb in tipsBreadcrumbs)
                breadcrumb.Unselect();

            if (index < tips.Count)
            {
                tips[index].gameObject.SetActive(true);
                tipsBreadcrumbs[index].Select();
            }
        }

        public void HideAllTips()
        {
            foreach (TipView? view in tips)
                view.gameObject.SetActive(false);
        }

        public async UniTask ShowTipWithFadeAsync(int index, float duration, CancellationToken ct)
        {
            ShowTip(index);

            Option<TipView> tipView = TipViewByIndex(index);

            if (tipView.Has == false)
            {
                ReportHub.LogError(ReportCategory.UI, $"View does not exist: {index}");
                return;
            }

            tipView.Value.RootCanvasGroup.alpha = 0f;

            await tipView.Value
                 .RootCanvasGroup.DOFade(1f, duration)
                 .ToUniTask(cancellationToken: ct);
        }

        public async UniTask HideTipWithFadeAsync(int index, float duration, CancellationToken ct)
        {
            Option<TipView> tipView = TipViewByIndex(index);

            if (tipView.Has == false)
            {
                ReportHub.LogError(ReportCategory.UI, $"View does not exist: {index}");
                return;
            }

            await tipView.Value
                 .RootCanvasGroup.DOFade(0f, duration)
                 .ToUniTask(cancellationToken: ct);

            // View might be cleared by ClearTips() during the await call. The existing reference is unreliable
            tipView = TipViewByIndex(index);

            if (tipView.Has)
                tipView.Value.gameObject.SetActive(false);
        }

        private Option<TipView> TipViewByIndex(int index)
        {
            if (index >= 0 && index < tips.Count)
                return Option<TipView>.Some(tips[index]);

            return Option<TipView>.None;
        }
    }
}
