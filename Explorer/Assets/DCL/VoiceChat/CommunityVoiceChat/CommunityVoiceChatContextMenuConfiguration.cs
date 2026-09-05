using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    //[CreateAssetMenu(fileName = "CommunityVoiceChatContextMenuSettings", menuName = "DCL/Communities/VoiceChat/ContextMenuSettings")]
    public class CommunityVoiceChatContextMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 250;
        [field: SerializeField] public int ElementsSpacing { get; private set; } = 5;
        [field: SerializeField] public int SeparatorHeight { get; private set; } = 20;
        [field: SerializeField] public RectOffset VerticalPadding { get; private set; } = null!;

        [field: SerializeField] public Sprite KickFromStreamSprite { get; private set; } = null!;
        [field: SerializeField] public string KickFromStreamText { get; private set; } = "Remove from stream";

        [field: SerializeField] public Sprite DemoteSpeakerSprite { get; private set; } = null!;
        [field: SerializeField] public string DemoteSpeakerText { get; private set; } = "Demote speaker";

        [field: SerializeField] public Sprite PromoteToSpeakerSprite { get; private set; } = null!;
        [field: SerializeField] public string PromoteToSpeakerText { get; private set; } = "Promote to speaker";

        [field: SerializeField] public Sprite BanUserSprite { get; private set; } = null!;
        [field: SerializeField] public string BanUserText { get; private set; } = "Ban from community";

        [field: SerializeField] public Sprite BanUserPopupSprite { get; private set; } = null!;
    }
}
