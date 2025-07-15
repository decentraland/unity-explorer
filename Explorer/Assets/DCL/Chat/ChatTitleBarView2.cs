using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace DCL.Chat
{
    public class ChatTitlebarView2 : MonoBehaviour
    {
        public event Action OnCloseRequested;
        public event Action OnMembersToggleRequested;

        public Button CloseChatButton => defaultTitlebarView.ButtonClose;
        public Button OpenMemberListButton => defaultTitlebarView.ButtonOpenMembers;
        public Button CloseMemberListButton => membersTitlebarView.ButtonClose;
        public Button BackFromMemberList => membersTitlebarView.ButtonBack;
        
        
        [Header("UI Elements")]
        [SerializeField] private CanvasGroup titlebarCanvasGroup;
        [SerializeField] public ChatDefaultTitlebarView defaultTitlebarView;
        [SerializeField] public ChatMemberTitlebarView membersTitlebarView;
        
        public void Initialize()
        {
            defaultTitlebarView.OnCloseRequested += () => OnCloseRequested?.Invoke();
            defaultTitlebarView.OnMembersRequested += () => OnMembersToggleRequested?.Invoke();
            membersTitlebarView.OnCloseRequested += () => OnCloseRequested?.Invoke();
            membersTitlebarView.OnBackRequested += () => OnMembersToggleRequested?.Invoke();
        }

        public void SetMemberListMode(bool isMemberListVisible)
        {
            defaultTitlebarView.Activate(!isMemberListVisible);
            membersTitlebarView.Activate(isMemberListVisible);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        public void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing)
        {
            titlebarCanvasGroup.DOKill();
            float targetAlpha = isFocused ? 1.0f : 0.0f;
            titlebarCanvasGroup.DOFade(targetAlpha, animate ? duration : 0f);
        }
    }
}