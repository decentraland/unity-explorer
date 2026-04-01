using UnityEngine;

namespace DCL.Chat.ChatReactions.Views
{
    public sealed class SituationalReactionView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform LaneRect { get; private set; } = null!;
    }
}
