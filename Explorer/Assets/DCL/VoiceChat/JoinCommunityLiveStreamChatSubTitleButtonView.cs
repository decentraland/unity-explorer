using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class JoinCommunityLiveStreamChatSubTitleButtonView : MonoBehaviour
    {
        [field: SerializeField] public Button JoinStreamButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ParticipantsAmount { get; private set; } = null!;
        [SerializeField] private CanvasGroup canvasGroup = null!;

        public void SetFocusedState(bool isFocused, bool animate, float duration)
        {
            canvasGroup.DOKill();
            float targetAlpha = isFocused ? 0.9f : 0.0f;
            canvasGroup.DOFade(targetAlpha, animate ? duration : 0f);
        }
    }
}
