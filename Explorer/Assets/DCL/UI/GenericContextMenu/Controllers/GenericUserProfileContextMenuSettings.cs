using UnityEngine;

namespace DCL.UI
{
    // We only need one of these, so its commented to reduce clutter
    //[CreateAssetMenu(fileName = "GenericUserProfileContextMenuSettings", menuName = "DCL/UI/Generic User Profile ContextMenu Settings")]
    public class GenericUserProfileContextMenuSettings : ScriptableObject
    {
        [field: SerializeField] public GenericContextMenuControlConfig BlockButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig JumpInButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig MentionButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig OpenUserProfileButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig OpenConversationButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig StartCallButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig InviteToCommunityConfig { get; private set; }
    }
}
