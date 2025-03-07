using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class ExplorePanelView : ViewBase, IView
    {
        private const float ANIMATION_SPEED = 0.2f;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform AnimationTransform { get; private set; } = null!;

        [field: SerializeField]
        public ExplorePanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public ProfileWidgetView ProfileWidget { get; private set; } = null!;

        [field: SerializeField]
        public ProfileMenuView ProfileMenuView { get; private set; } = null!;

        [field: SerializeField]
        public ProfileMenuView SkyMenuView { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig? BackgroundMusic { get; private set; }
        [field: SerializeField]
        public AudioClipConfig? OpenMenu { get; private set; }
        [field: SerializeField]
        public AudioClipConfig? CloseMenu { get; private set; }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            CanvasGroup.alpha = 0;
            UIAudioEventsBus.Instance.SendPlayContinuousAudioEvent(BackgroundMusic);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(OpenMenu);
            return CanvasGroup.DOFade(1, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendStopPlayingContinuousAudioEvent(BackgroundMusic);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(CloseMenu);
            return CanvasGroup.DOFade(0, ANIMATION_SPEED).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }
    }

    [Serializable]
    public struct ExplorePanelTabSelectorMapping
    {
        [field: SerializeField]
        public TabSelectorView TabSelectorViews { get; private set; }

        [field: SerializeField]
        public ExploreSections Section { get; private set; }
    }
}
