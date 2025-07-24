using System;
using DCL.Chat;
using DCL.Chat.ChatViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatDefaultTitlebarView : MonoBehaviour
{
    public event Action OnCloseRequested;
    public event Action OnMembersRequested;
    public event Action OnContextMenuRequested;
    public event Action OnProfileContextMenuRequested;

    public Button ButtonClose => buttonClose;
    public Button ButtonOpenMembers => buttonOpenMembers;
    public Button ButtonOpenContextMenu => buttonOpenContextMenu;
    public Button ButtonOpenProfileContextMenu => buttonOpenProfileContextMenu;
    
    [SerializeField] private Button buttonClose;
    [SerializeField] private Button buttonOpenMembers;
    [SerializeField] private Button buttonOpenContextMenu;
    [SerializeField] private Button buttonOpenProfileContextMenu;
    [SerializeField] private TMP_Text textChannelName;
    [SerializeField] private TMP_Text textMembersCount;
    [SerializeField] private ChatProfileView chatProfileView;
    [SerializeField] private GameObject nearbyElementsContainer;

    private void Awake()
    {
        buttonOpenContextMenu.onClick.AddListener(() => OnContextMenuRequested?.Invoke());
        buttonOpenProfileContextMenu.onClick.AddListener(() => OnProfileContextMenuRequested?.Invoke());
        buttonClose.onClick.AddListener(() => OnCloseRequested?.Invoke());
        buttonOpenMembers.onClick.AddListener(() => OnMembersRequested?.Invoke());
    }

    public void Setup(ChatTitlebarViewModel model)
    {
        textChannelName.text = model.Username;

        bool shouldShowMembersButton = model.ViewMode == TitlebarViewMode.Nearby ||
                                       model.ViewMode == TitlebarViewMode.Community;

        buttonOpenMembers.gameObject.SetActive(shouldShowMembersButton);

        if (model.IsLoadingProfile)
        {
            chatProfileView.gameObject.SetActive(false);
            nearbyElementsContainer.SetActive(false);
            if (model.ViewMode == TitlebarViewMode.DirectMessage)
                buttonOpenMembers.gameObject.SetActive(false);
            return;
        }

        bool showProfile = model.ViewMode == TitlebarViewMode.DirectMessage ||
                           model.ViewMode == TitlebarViewMode.Community;

        chatProfileView.gameObject.SetActive(showProfile);
        nearbyElementsContainer.SetActive(model.ViewMode == TitlebarViewMode.Nearby);

        if (showProfile)
        {
            chatProfileView.Setup(model);
        }

        buttonOpenProfileContextMenu.interactable
            = model.ViewMode == TitlebarViewMode.DirectMessage;
    }
    
    public void SetMemberCount(string count) => textMembersCount.text = count;
    public void Activate(bool activate) => gameObject.SetActive(activate);
}
