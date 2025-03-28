using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controllers
{
    [CreateAssetMenu(fileName = "GenericUserProfileContextMenuSettings", menuName = "DCL/UI/Generic User Profile ContextMenu Settings")]
    public class GenericUserProfileContextMenuSettings : ScriptableObject
    {
        [field: SerializeField] public GenericContextMenuControlConfig BlockButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig JumpInButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig MentionButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig OpenUserProfileButtonConfig { get; private set; }
        [field: SerializeField] public GenericContextMenuControlConfig OpenConversationButtonConfig { get; private set; }
    }
}
