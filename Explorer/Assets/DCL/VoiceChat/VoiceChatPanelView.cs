using DCL.VoiceChat.CommunityVoiceChat;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelView : MonoBehaviour
    {
        [field: SerializeField] public VoiceChatView VoiceChatView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelResizeView VoiceChatPanelResizeView { get; private set; } = null!;
        [field: SerializeField] public CommunityVoiceChatTitlebarView CommunityVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public SceneVoiceChatTitlebarView SceneVoiceChatTitlebarView { get; private set; } = null!;

    }
}
