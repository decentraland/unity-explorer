using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public sealed class SituationalReactionView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform LaneRect { get; private set; } = null!;
    }
}
