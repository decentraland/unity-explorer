using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class ExplorePanelView : ViewBase, IView
    {
        [field: SerializeField]
        public RectTransform AnimationTransform { get; private set; } = null!;

        [field: SerializeField]
        public ExplorePanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public ProfileWidgetView ProfileWidget { get; private set; } = null!;

        [field: SerializeField]
        public SystemMenuView SystemMenu { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig? BackgroundMusic { get; private set; }
        [field: SerializeField]
        public AudioClipConfig? OpenMenu { get; private set; }
        [field: SerializeField]
        public AudioClipConfig? CloseMenu { get; private set; }
        [field: SerializeField]
        public AudioMixerSnapshot? MuteSoundsSnapshot { get; private set; }
        [field: SerializeField]
        public AudioMixerSnapshot? RestoreSoundsSnapShot { get; private set; }


        private bool snapshotsPresent;

        private void Awake()
        {
            snapshotsPresent = MuteSoundsSnapshot != null && RestoreSoundsSnapShot != null;
        }

        protected override UniTask PlayShowAnimation(CancellationToken ct)
        {
            if (snapshotsPresent) { MuteSoundsSnapshot.TransitionTo(2); }
            UIAudioEventsBus.Instance.SendPlayLoopingAudioEvent(BackgroundMusic);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(OpenMenu);
            AnimationTransform.anchoredPosition = new Vector2(0, canvas.pixelRect.width);
            return AnimationTransform.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimation(CancellationToken ct)
        {
            if (snapshotsPresent) { RestoreSoundsSnapShot.TransitionTo(2); }
            UIAudioEventsBus.Instance.SendStopPlayingLoopingAudioEvent(BackgroundMusic);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(CloseMenu);
            AnimationTransform.anchoredPosition = Vector2.zero;
            return AnimationTransform.DOAnchorPos(new Vector2(canvas.pixelRect.width, 0), 0.5f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
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
