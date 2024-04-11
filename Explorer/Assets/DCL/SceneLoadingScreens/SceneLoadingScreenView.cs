using Cysharp.Threading.Tasks;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Random = UnityEngine.Random;

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
        public Button ShowNextButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ShowPreviousButton { get; private set; } = null!;

        [SerializeField]
        private Transform tipsParent = null!;

        [SerializeField]
        private TipView tipViewPrefab = null!;

        [SerializeField]
        private Sprite[] fallbackSprites = null!;

        [field: SerializeField]
        public Image Background { get; private set; }

        [SerializeField]
        private TipBreadcrumb breadcrumbPrefab = null!;

        [SerializeField]
        private Transform breadcrumbParent = null!;

        public event Action<int>? OnBreadcrumbClicked;

        private readonly List<TipView> tips = new ();
        private readonly List<TipBreadcrumb> tipsBreadcrumbs = new ();

        public void ClearTips()
        {
            foreach (TipView tip in tips)
                Destroy(tip.gameObject);

            foreach (TipBreadcrumb? breadcrumb in tipsBreadcrumbs)
                Destroy(breadcrumb.gameObject);

            tips.Clear();
            tipsBreadcrumbs.Clear();
        }

        public void AddTip(SceneTips.Tip tip)
        {
            TipView view = Instantiate(tipViewPrefab, tipsParent);
            view.TitleLabel.text = tip.Title;
            view.BodyLabel.text = tip.Body;

            Sprite? sprite = tip.Image;

            Sprite icon = sprite != null
                ? sprite
                : fallbackSprites[Random.Range(0, fallbackSprites.Length)];

            view.Image.sprite = icon;

            TipBreadcrumb breadcrumb = Instantiate(breadcrumbPrefab, breadcrumbParent);
            int breadcrumbIndex = tipsBreadcrumbs.Count;
            breadcrumb.Button.onClick.AddListener(() => OnBreadcrumbClicked?.Invoke(breadcrumbIndex));

            tips.Add(view);
            tipsBreadcrumbs.Add(breadcrumb);
        }

        public void ShowTip(int index)
        {
            tips[index].gameObject.SetActive(true);

            foreach (TipBreadcrumb? breadcrumb in tipsBreadcrumbs)
                breadcrumb.Unselect();

            tipsBreadcrumbs[index].Select();
        }

        public void HideAllTips()
        {
            foreach (TipView? view in tips)
                view.gameObject.SetActive(false);
        }

        public async UniTask ShowTipWithFadeAsync(int index, float duration, CancellationToken ct)
        {
            ShowTip(index);

            tips[index].RootCanvasGroup.alpha = 0f;

            await tips[index]
                 .RootCanvasGroup.DOFade(1f, duration)
                 .ToUniTask(cancellationToken: ct);
        }

        public async UniTask HideTipWithFadeAsync(int index, float duration, CancellationToken ct)
        {
            await tips[index]
                 .RootCanvasGroup.DOFade(0f, duration)
                 .ToUniTask(cancellationToken: ct);

            tips[index].gameObject.SetActive(false);
        }
    }
}
