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

        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig BackgroundMusic;
        [field: SerializeField]
        public AudioMixerSnapshot MuteSoundsSnapshot;
        [field: SerializeField]
        public AudioMixerSnapshot RestoreSoundsSnapShot;


        protected override UniTask PlayShowAnimation(CancellationToken ct)
        {
            MuteSoundsSnapshot.TransitionTo(2);
            UIAudioEventsBus.Instance.SendPlayLoopingAudioEvent(BackgroundMusic);
            AnimationTransform.anchoredPosition = new Vector2(0, canvas.pixelRect.width);
            return AnimationTransform.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
        }

        protected override UniTask PlayHideAnimation(CancellationToken ct)
        {
            RestoreSoundsSnapShot.TransitionTo(2);
            UIAudioEventsBus.Instance.SendStopPlayingLoopingAudioEvent(BackgroundMusic);
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
