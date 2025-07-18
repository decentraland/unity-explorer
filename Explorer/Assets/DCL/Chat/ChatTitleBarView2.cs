using System;
using DCL.Chat.Services;
using DCL.Web3;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using MVC;

namespace DCL.Chat
{
    public class ChatTitlebarView2 : MonoBehaviour
    {
        public event Action OnCloseRequested;
        public event Action OnMembersToggleRequested;
        public event Action<ChatContextMenuRequest> OnContextMenuRequested;
        public event Action<UserProfileMenuRequest> OnProfileContextMenuRequested;

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
            defaultTitlebarView.OnContextMenuRequested += OnContextMenuButtonClicked;
            defaultTitlebarView.OnProfileContextMenuRequested += OnProfileContextMenuClicked;
            defaultTitlebarView.OnCloseRequested += () => OnCloseRequested?.Invoke();
            defaultTitlebarView.OnMembersRequested += () => OnMembersToggleRequested?.Invoke();
            membersTitlebarView.OnCloseRequested += () => OnCloseRequested?.Invoke();
            membersTitlebarView.OnBackRequested += () => OnMembersToggleRequested?.Invoke();
            
        }

        private void OnContextMenuButtonClicked()
        {
            var data = new ChatContextMenuRequest
            {
                Position = defaultTitlebarView.ButtonOpenContextMenu.transform.position
            };

            OnContextMenuRequested?.Invoke(data);
        }

        private void OnProfileContextMenuClicked()
        {
            var data = new UserProfileMenuRequest
            {
                WalletAddress = new Web3Address(""), Position = defaultTitlebarView.ButtonOpenProfileContextMenu.transform.position, AnchorPoint = MenuAnchorPoint.TOP_RIGHT, Offset = Vector2.zero
            };
            OnProfileContextMenuRequested?.Invoke(data);
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