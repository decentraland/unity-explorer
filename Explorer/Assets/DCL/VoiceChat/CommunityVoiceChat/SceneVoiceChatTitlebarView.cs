using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class SceneVoiceChatTitlebarView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;
        [field: SerializeField] public CanvasGroup VoiceChatCanvasGroup { get; private set; }
        [field: SerializeField] public GameObject VoiceChatContainer { get; private set; }
        [field: SerializeField] public SceneVoiceChatActiveCallView SceneVoiceChatActiveCallView { get; private set; }
    }
}
