using UnityEngine;

namespace DCL.Chat.ChatReactions.Views
{
    public sealed class SituationalReactionView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform LaneRect { get; private set; } = null!;

        // Wired in the prefab to the chat shared-area Canvas (the one MVC's SetAllViewsCanvasActive toggles).
        // The UI-lane renderer reads this to skip its draw while the UI is hidden (photo mode, Show/Hide UI).
        [field: SerializeField] public Canvas LaneCanvas { get; private set; } = null!;
    }
}
