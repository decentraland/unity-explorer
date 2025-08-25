using DCL.UIToolkit.Elements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    [UxmlElement]
    public partial class NametagElement : VisualElement
    {
        private const string USS_BLOCK = "nametag";

        private const string USS_SHOW_MESSAGE = USS_BLOCK + "--show-message";
        private const string USS_VERIFIED = USS_BLOCK + "--verified";
        private const string USS_OFFICIAL = USS_BLOCK + "--official";
        private const string USS_VOICE_CHAT = USS_BLOCK + "--voice-chat";
        private const string USS_DM = USS_BLOCK + "--dm";
        private const string USS_MENTION = USS_BLOCK + "--mention";
        private const string USS_COMMUNITY = USS_BLOCK + "--community";

        private const string USS_BACKGROUND = USS_BLOCK + "__background";
        private const string USS_BACKGROUND_CENTER = USS_BLOCK + "__background-center";
        private const string USS_BACKGROUND_MENTION = USS_BLOCK + "__background-mention";
        private const string USS_HEADER = USS_BLOCK + "__header";
        private const string USS_USERNAME = USS_BLOCK + "__username";
        private const string USS_COMMUNITY_NAME_CONTAINER = USS_BLOCK + "__community-name-container";
        private const string USS_COMMUNITY_NAME = USS_BLOCK + "__community-name";
        private const string USS_POINTER = USS_BLOCK + "__pointer";

        private const string USS_BADGE_VERIFIED = USS_BLOCK + "__badge-verified";
        private const string USS_BADGE_DM = USS_BLOCK + "__badge-dm";
        private const string USS_BADGE_DM_TAG = USS_BLOCK + "__badge-dm-tag";
        private const string USS_BADGE_DM_RECIPIENT = USS_BLOCK + "__badge-dm-recipient";
        private const string USS_BADGE_OFFICIAL = USS_BLOCK + "__badge-official";
        private const string USS_BADGE_VOICE_CHAT = USS_BLOCK + "__badge-voice-chat";
        private const string USS_BADGE_VOICE_CHAT_MIDDLE = USS_BLOCK + "__badge-voice-chat-middle";
        private const string USS_BADGE_VOICE_CHAT_SIDE = USS_BLOCK + "__badge-voice-chat-side";
        private const string USS_BADGE_VOICE_CHAT_ALT = USS_BLOCK + "__badge-voice-chat--alt";

        private const string USS_MESSAGE = USS_BLOCK + "__message";
        private const string USS_MESSAGE_CONTAINER = USS_BLOCK + "__message-container";

        private const string USS_DEBUG_LABEL = USS_BLOCK + "__debug-label";

        /// <summary>
        /// This value represents the last calculated Sqr Distance to the camera,
        /// we use this to avoid recalculating transparency and scale when distance hasn't changed.
        /// </summary>
        public float LastSqrDistance { get; set; }
        public string ProfileID { get; set; }
        public int ProfileVersion { get; set; }

        [UxmlAttribute]
        public string Username
        {
            get => usernameLabel.text;
            set => usernameLabel.text = value;
        }

        [UxmlAttribute]
        public bool Verified
        {
            get => ClassListContains(USS_VERIFIED);
            set => EnableInClassList(USS_VERIFIED, value);
        }

        [UxmlAttribute]
        public bool Official
        {
            get => ClassListContains(USS_OFFICIAL);
            set => EnableInClassList(USS_OFFICIAL, value);
        }

        [UxmlAttribute]
        public bool VoiceChat
        {
            get => ClassListContains(USS_VOICE_CHAT);
            set => EnableInClassList(USS_VOICE_CHAT, value);
        }

        [UxmlAttribute]
        public bool ShowMessage
        {
            get => ClassListContains(USS_SHOW_MESSAGE);

            set
            {
                EnableInClassList(USS_SHOW_MESSAGE, value);
                SetMessageSize(!value, false);
            }
        }

        [UxmlAttribute]
        public bool DM
        {
            get => ClassListContains(USS_DM);
            set => EnableInClassList(USS_DM, value);
        }

        [UxmlAttribute]
        public bool Mention
        {
            get => ClassListContains(USS_MENTION);
            set => EnableInClassList(USS_MENTION, value);
        }

        [UxmlAttribute]
        public bool Community
        {
            get => ClassListContains(USS_COMMUNITY);
            set => EnableInClassList(USS_COMMUNITY, value);
        }

        [UxmlAttribute]
        public string CommunityName
        {
            get => communityName.text;
            set => communityName.text = value;
        }

        [UxmlAttribute, TextArea]
        public string MessageText
        {
            get => messageLabel.text;

            set
            {
                messageLabel.text = value;
                if (ShowMessage) SetMessageSize();
            }
        }

        [UxmlAttribute]
        public string DMRecipient
        {
            get => dmRecipientLabel.text;
            set => dmRecipientLabel.text = value;
        }

        [UxmlAttribute]
        public string DebugText
        {
            get => debugLabel.text;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (debugLabel.panel != null) debugLabel.RemoveFromHierarchy();
                }
                else
                {
                    if (debugLabel.panel == null) this.Insert(0, debugLabel);
                }

                debugLabel.text = value;
            }
        }

        private readonly VisualElement header;
        private readonly Label usernameLabel;
        private readonly Label communityName;
        private readonly VisualElement dmBadge;
        private readonly Label dmRecipientLabel;
        private readonly VisualElement voiceChatBadge;

        private readonly VisualElement messageContainer;
        private readonly Label messageLabel;

        private readonly IVisualElementScheduledItem hideMessage;

        private readonly Label debugLabel;

        public NametagElement()
        {
            AddToClassList(USS_BLOCK);
            usageHints = UsageHints.DynamicTransform;

            var pointer = new VisualElement { name = "pointer" };
            Add(pointer);
            pointer.AddToClassList(USS_POINTER);
            pointer.usageHints = UsageHints.DynamicColor;

            var background = new VisualElement { name = "background" };
            Add(background);
            background.AddToClassList(USS_BACKGROUND);

            {
                var backgroundMention = new GradientElement { name = "background-mention" };
                background.Add(backgroundMention);
                backgroundMention.AddToClassList(USS_BACKGROUND_MENTION);
                backgroundMention.Vertical = true;

                var backgroundCenter = new VisualElement { name = "background-center" };
                background.Add(backgroundCenter);
                backgroundCenter.AddToClassList(USS_BACKGROUND_CENTER);
            }

            var communityNameContainer = new VisualElement { name = "community-name-container" };
            Add(communityNameContainer);
            communityNameContainer.AddToClassList(USS_COMMUNITY_NAME_CONTAINER);

            {
                communityNameContainer.Add(communityName = new Label("Community Name") { name = "community-name" });
                communityName.AddToClassList(USS_COMMUNITY_NAME);
            }

            Add(header = new VisualElement { name = "header" });
            header.AddToClassList(USS_HEADER);

            {
                header.Add(usernameLabel = new Label("Squeazer") { name = "username" });
                usernameLabel.AddToClassList(USS_USERNAME);

                var verifiedBadge = new VisualElement { name = "verified-badge" };
                header.Add(verifiedBadge);
                verifiedBadge.AddToClassList(USS_BADGE_VERIFIED);

                var officialBadge = new VisualElement { name = "official-badge" };
                header.Add(officialBadge);
                officialBadge.AddToClassList(USS_BADGE_OFFICIAL);

                header.Add(voiceChatBadge = new VisualElement { name = "voice-chat-badge" });
                voiceChatBadge.AddToClassList(USS_BADGE_VOICE_CHAT);

                {
                    var leftSide = new VisualElement { name = "left-side" };
                    voiceChatBadge.Add(leftSide);
                    leftSide.AddToClassList(USS_BADGE_VOICE_CHAT_SIDE);

                    var middle = new VisualElement { name = "middle" };
                    voiceChatBadge.Add(middle);
                    middle.AddToClassList(USS_BADGE_VOICE_CHAT_MIDDLE);

                    var rightSide = new VisualElement { name = "left-side" };
                    voiceChatBadge.Add(rightSide);
                    rightSide.AddToClassList(USS_BADGE_VOICE_CHAT_SIDE);

                    // We listen on the middle element since voiceChatBadge has no transition itself
                    // NOTE: This might need optimization
                    middle.RegisterCallback<TransitionEndEvent, NametagElement>((_, ne) =>
                    {
                        if (ne.ClassListContains(USS_VOICE_CHAT))
                            ne.voiceChatBadge.ToggleInClassList(USS_BADGE_VOICE_CHAT_ALT);
                        else
                            ne.voiceChatBadge.RemoveFromClassList(USS_BADGE_VOICE_CHAT_ALT);
                    }, this);
                }

                header.Add(dmBadge = new VisualElement { name = "dm-badge" });
                dmBadge.AddToClassList(USS_BADGE_DM);

                {
                    var dmTag = new Label("DM") { name = "dm-tag" };
                    dmBadge.Add(dmTag);
                    dmTag.AddToClassList(USS_BADGE_DM_TAG);

                    dmBadge.Add(dmRecipientLabel = new Label("for Jack O'Neill") { name = "dm-badge-recipient" });
                    dmRecipientLabel.AddToClassList(USS_BADGE_DM_RECIPIENT);
                }
            }

            Add(messageContainer = new VisualElement { name = "message-container" });
            messageContainer.AddToClassList(USS_MESSAGE_CONTAINER);

            {
                messageContainer.Add(messageLabel = new Label { name = "message" });
                messageLabel.AddToClassList(USS_MESSAGE);

                hideMessage = this.schedule.Execute(() => { ShowMessage = false; });
                hideMessage.Pause();
            }

            debugLabel = new Label { name = "debug-text" };
            debugLabel.AddToClassList(USS_DEBUG_LABEL);
        }

        public void SetData(string username, Color usernameColor, string walletId, bool verified, bool official)
        {
            Username = BuildName(username, walletId, verified);
            usernameLabel.style.color = usernameColor;

            Verified = verified;
            Official = official;
        }

        public void DisplayMessage(string chatMessage, bool isMention, bool isPrivateMessage, bool isOwnMessage, string recipientValidatedName,
            string recipientWalletId, Color recipientNameColor, bool isCommunityMessage, string communityNameText)
        {
            MessageText = chatMessage;
            DMRecipient = isOwnMessage ? BuildRecipientName(recipientValidatedName, recipientWalletId, string.IsNullOrEmpty(recipientWalletId)) : string.Empty;
            dmRecipientLabel.style.color = recipientNameColor;
            DM = isPrivateMessage;
            Mention = isMention;
            Community = isCommunityMessage;
            CommunityName = communityNameText;
            ShowMessage = true;
            hideMessage.ExecuteLater(NametagViewConstants.CHAT_BUBBLE_DELAY);
        }

        private void SetMessageSize(bool zero = false, bool label = true, bool container = true)
        {
            float dmBadgeSize = DM ? -dmBadge.resolvedStyle.width + 31 + dmRecipientLabel.MeasureTextSize(dmRecipientLabel.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x : 0;
            float headerTargetSize = header.resolvedStyle.width + dmBadgeSize;

            Vector2 size = zero ? Vector2.zero : messageLabel.MeasureTextSize(messageLabel.text, Mathf.Max(headerTargetSize, NametagViewConstants.MAX_MESSAGE_WIDTH), MeasureMode.AtMost, 0, MeasureMode.Undefined);

            if (label)
            {
                messageLabel.style.width = size.x;
                messageLabel.style.height = size.y;
            }

            if (container)
            {
                // When easing to 0 if we use "back" easing the negative value breaks layouting for a frame
                messageContainer.EnableInClassList("safe-easing", zero);

                messageContainer.schedule.Execute(() =>
                                 {
                                     messageContainer.style.width = size.x;
                                     messageContainer.style.height = size.y + (zero ? 0f : messageLabel.resolvedStyle.marginTop);
                                 })
                                .StartingIn(100);
            }
        }

        private string BuildName(string username, string walletId, bool hasClaimedName) =>
            hasClaimedName ? username : $"{username}{NametagViewConstants.WALLET_ID_OPENING_STYLE}{walletId}{NametagViewConstants.WALLET_ID_CLOSING_STYLE}";

        private string BuildRecipientName(string username, string walletId, bool hasClaimedName) =>
            string.Concat(NametagViewConstants.RECIPIENT_NAME_START_STRING, hasClaimedName ? username : $"{username}{NametagViewConstants.WALLET_ID_OPENING_STYLE}{walletId}{NametagViewConstants.WALLET_ID_CLOSING_STYLE}");
    }
}
