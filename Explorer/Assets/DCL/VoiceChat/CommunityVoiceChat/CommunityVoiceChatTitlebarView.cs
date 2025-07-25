using DCL.Communities.CommunitiesCard.Members;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatTitlebarView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        public event Action CollapseButtonClicked;

        [field: SerializeField]
        public CanvasGroup VoiceChatCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject VoiceChatContainer { get; private set; }

        [field: SerializeField]
        public Button CollapseButton  { get; private set; }

        [field: SerializeField]
        public Sprite CollapseButtonImage { get; private set; }

        [field: SerializeField]
        public Sprite UnCollapseButtonImage { get; private set; }

        [field: SerializeField]
        public RectTransform HeaderContainer { get; private set; }

        [field: SerializeField]
        public RectTransform ContentContainer { get; private set; }

        [field: SerializeField]
        public RectTransform FooterContainer { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatInCallView CommunityVoiceChatInCallView { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatSearchView CommunityVoiceChatSearchView { get; private set; }


        [field: Header("Assets")]
        [field: SerializeField] private CommunityVoiceChatContextMenuConfiguration contextMenuSettings = null!;

        public event Action<UserProfileContextMenuControlSettings.UserData, UserProfileContextMenuControlSettings.FriendshipStatus>? ContextMenuUserProfileButtonClicked;
        public event Action<VoiceChatParticipantsStateService.ParticipantState>? DemoteSpeaker;
        public event Action<VoiceChatParticipantsStateService.ParticipantState>? PromoteToSpeaker;
        public event Action<VoiceChatParticipantsStateService.ParticipantState>? Kick;
        public event Action<VoiceChatParticipantsStateService.ParticipantState>? Ban;

        private GenericContextMenu? contextMenu;
        private UserProfileContextMenuControlSettings? userProfileContextMenuControlSettings;
        private GenericContextMenuElement? demoteSpeakerButton;
        private GenericContextMenuElement? promoteToSpeakerButton;
        private GenericContextMenuElement? kickFromStreamButton;
        private GenericContextMenuElement? banFromCommunityButton;
        private VoiceChatParticipantsStateService.ParticipantState lastClickedProfile;
        private CancellationToken cancellationToken;

        private void Start()
        {
            cancellationToken = new CancellationToken();
            CollapseButton.onClick.AddListener(() => CollapseButtonClicked?.Invoke());

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding, elementsSpacing: contextMenuSettings.ElementsSpacing)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings((user, friendshipStatus) => ContextMenuUserProfileButtonClicked?.Invoke(user, friendshipStatus)))
                         .AddControl(new SeparatorContextMenuControlSettings(contextMenuSettings.SeparatorHeight, -contextMenuSettings.VerticalPadding.left, -contextMenuSettings.VerticalPadding.right))
                         .AddControl(demoteSpeakerButton = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.DemoteSpeakerText, contextMenuSettings.DemoteSpeakerSprite, () => DemoteSpeaker?.Invoke(lastClickedProfile!))))
                         .AddControl(promoteToSpeakerButton = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.PromoteToSpeakerText, contextMenuSettings.PromoteToSpeakerSprite, () => PromoteToSpeaker?.Invoke(lastClickedProfile!))))
                         .AddControl(kickFromStreamButton = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.KickFromStreamText, contextMenuSettings.KickFromStreamSprite, () => Kick?.Invoke(lastClickedProfile!))))
                         .AddControl(banFromCommunityButton = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BanUserText, contextMenuSettings.BanUserSprite, () => Ban?.Invoke(lastClickedProfile!))));
        }

        private void OnContextMenuButtonClicked(VoiceChatParticipantsStateService.ParticipantState voiceChatMember, Vector2 buttonPosition, PlayerEntryView elementView)
        {
            lastClickedProfile = voiceChatMember;

            promoteToSpeakerButton!.Enabled = !voiceChatMember.IsSpeaker;
            demoteSpeakerButton!.Enabled = voiceChatMember.IsSpeaker;
            kickFromStreamButton!.Enabled = true;
            banFromCommunityButton!.Enabled = true;

            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, buttonPosition), cancellationToken);
        }

        public void ConfigureEntry(PlayerEntryView entryView, VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            entryView.SubscribeToInteractions(OnContextMenuButtonClicked);
            entryView.SetUserProfile(participantState);
        }

        public void SetCollapsedButtonState(bool isCollapsed)
        {
            ContentContainer.gameObject.SetActive(!isCollapsed);
            FooterContainer.gameObject.SetActive(!isCollapsed);
            CollapseButton.image.sprite = isCollapsed ? UnCollapseButtonImage : CollapseButtonImage;
        }

        public void Show()
        {
            VoiceChatContainer.SetActive(true);
            VoiceChatCanvasGroup.alpha = 0;
            VoiceChatCanvasGroup
               .DOFade(1, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(true);
                    VoiceChatCanvasGroup.alpha = 1;
                });
        }

        public void Hide()
        {
            VoiceChatCanvasGroup.alpha = 1;
            VoiceChatCanvasGroup
               .DOFade(0, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(false);
                    VoiceChatCanvasGroup.alpha = 0;
                });
        }
    }
}
