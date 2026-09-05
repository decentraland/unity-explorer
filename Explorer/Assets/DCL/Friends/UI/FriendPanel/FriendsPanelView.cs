using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Friends.UI.FriendPanel.Sections.Blocked;
using DCL.Friends.UI.FriendPanel.Sections.Friends;
using DCL.Friends.UI.FriendPanel.Sections.Requests;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel
{
    public class FriendsPanelView: ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }

        [field: Header("Friends tab")]
        [field: SerializeField] public Button FriendsTabButton { get; private set; }
        [field: SerializeField] public GameObject FriendsTabSelected { get; private set; }
        [field: SerializeField] public FriendsSectionView FriendsSection { get; private set; }

        [field: Header("Requests tab")]
        [field: SerializeField] public Button RequestsTabButton { get; private set; }
        [field: SerializeField] public GameObject RequestsTabSelected { get; private set; }
        [field: SerializeField] public RequestsSectionView RequestsSection { get; private set; }

        [field: Header("Blocked tab")]
        [field: SerializeField] public Button BlockedTabButton { get; private set; }
        [field: SerializeField] public GameObject BlockedTabSelected { get; private set; }
        [field: SerializeField] public BlockedSectionView BlockedSection { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig? OpenPanel { get; private set; }
        [field: SerializeField] public AudioClipConfig? ClosePanel { get; private set; }

        protected override UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(OpenPanel);
            return base.PlayShowAnimationAsync(ct);
        }

        protected override UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClosePanel);
            return base.PlayHideAnimationAsync(ct);
        }
    }
}
