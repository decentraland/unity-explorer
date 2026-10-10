using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatEntryConfiguration", menuName = "DCL/Chat/Chat Entry Configuration")]
    public class ChatEntryConfigurationSO : ScriptableObject
    {
        [field: SerializeField] public float BackgroundHeightOffset { private set; get; } = 56;
        [field: SerializeField] public float BackgroundWidthOffset { private set; get; } = 56;
        [field: SerializeField] public float MaxEntryWidth { private set; get; } = 246;
        [field: SerializeField] public float VerifiedBadgeWidth { private set; get; } = 15;
    }
}
