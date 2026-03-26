using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceChatButtonView : MonoBehaviour
    {
        [Serializable]
        public struct MetaStateSprites
        {
            public Sprite unselected;
            public Sprite hover;
        }

        [SerializeField] private Button? button;
        [SerializeField] private Image? unselectedImage;
        [SerializeField] private Image? hoverStateImage;

        [Space]
        [SerializeField] private MetaStateSprites disconnectedSprites;
        [SerializeField] private MetaStateSprites hearingSprites;
        [SerializeField] private MetaStateSprites speakingSprites;
        [SerializeField] private MetaStateSprites blockedSprites;

        public Button? Button => button;

        private void Awake()
        {
            SetState(ProximityVoiceChatState.Disconnected);
        }

        public void SetState(ProximityVoiceChatState state)
        {
            MetaStateSprites sprites = state switch
                                       {
                                           ProximityVoiceChatState.Disconnected => disconnectedSprites,
                                           ProximityVoiceChatState.Hearing => hearingSprites,
                                           ProximityVoiceChatState.Speaking => speakingSprites,
                                           ProximityVoiceChatState.Blocked => blockedSprites,
                                           _ => disconnectedSprites,
                                       };

            unselectedImage!.sprite = sprites.unselected;
            hoverStateImage!.sprite = sprites.hover;
        }
    }
}
