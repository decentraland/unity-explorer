using System;
using DCL.Chat;
using DCL.Chat.ChatViewModels;
using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatDefaultTitlebarView : MonoBehaviour
{
    public event Action? OnCloseRequested;
    public event Action? OnMembersRequested;
    public event Action? OnContextMenuRequested;
    public event Action<TitlebarViewMode>? OnProfileContextMenuRequested;

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

    private TitlebarViewMode currentViewMode;
    
    private void Awake()
    {
        buttonOpenContextMenu.onClick.AddListener(() => OnContextMenuRequested?.Invoke());
        buttonOpenProfileContextMenu.onClick.AddListener(() => OnProfileContextMenuRequested?.Invoke(currentViewMode));
        buttonClose.onClick.AddListener(() => OnCloseRequested?.Invoke());
        buttonOpenMembers.onClick.AddListener(() => OnMembersRequested?.Invoke());
    }

    public void Setup(ChatTitlebarViewModel model)
    {
        currentViewMode = model.ViewMode;
        textChannelName.text = model.Username;

        bool shouldShowMembersButton = model.ViewMode == TitlebarViewMode.Nearby ||
                                       model.ViewMode == TitlebarViewMode.Community;

        buttonOpenMembers.gameObject.SetActive(shouldShowMembersButton);

        if (model.Thumbnail.Value.ThumbnailState == ProfileThumbnailViewModel.State.LOADING)
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


        if (model.ViewMode == TitlebarViewMode.Community ||
            model.ViewMode == TitlebarViewMode.DirectMessage)
        {
            buttonOpenProfileContextMenu.interactable = true;
        }
        else
        {
            buttonOpenProfileContextMenu.interactable = false;
        }
    }

    public void SetMemberCount(string count) => textMembersCount.text = count;
    public void Activate(bool activate) => gameObject.SetActive(activate);
}
