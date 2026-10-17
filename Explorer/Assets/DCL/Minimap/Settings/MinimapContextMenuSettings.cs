using UnityEngine;

namespace DCL.Minimap.Settings
{
    /// <summary>
    /// Layout configuration for the context menu that appears when pressing the 3 dots button at the top-right corner of the Minimap.
    /// </summary>
    [CreateAssetMenu(fileName = "MinimapContextMenuSettings", menuName = "DCL/Minimap/MinimapContextMenuSettings")]
    public class MinimapContextMenuSettings : ScriptableObject
    {
        [field: Header("Set as Home")]
        [field: SerializeField] public string SetAsHomeText { get; private set; } = "Set as Home";

        [field: Header("Copy Link")]
        [field: SerializeField] public Sprite CopyLinkSprite { get; private set; } = null!;
        [field: SerializeField] public string CopyLinkText { get; private set; } = "Copy Link";

        [field: Header("Reload Scene")]
        [field: SerializeField] public Sprite ReloadSceneSprite { get; private set; } = null!;
        [field: SerializeField] public string ReloadSceneText { get; private set; } = "Reload Scene";
    }
}
