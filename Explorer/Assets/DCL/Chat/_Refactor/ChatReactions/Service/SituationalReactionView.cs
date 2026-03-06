using UnityEngine;

namespace DCL.Chat.Reactions
{
    public sealed class SituationalReactionView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform LaneRect { get; private set; } = null!;
    }
}
