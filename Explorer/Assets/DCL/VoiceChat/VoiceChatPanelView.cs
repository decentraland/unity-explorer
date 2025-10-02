using DCL.VoiceChat.CommunityVoiceChat;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelView : MonoBehaviour
    {
        [field: SerializeField] public PrivateVoiceChatView PrivateVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelResizeView VoiceChatPanelResizeView { get; private set; } = null!;
        [field: SerializeField] public CommunityVoiceChatPanelView CommunityVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public SceneVoiceChatPanelView SceneVoiceChatPanelView { get; private set; } = null!;

    }
}
