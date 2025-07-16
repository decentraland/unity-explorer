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

    public Button ButtonClose => buttonClose;
    public Button ButtonOpenMembers => buttonOpenMembers;
    
    [SerializeField] private Button buttonClose;
    [SerializeField] private Button buttonOpenMembers;
    [SerializeField] private TMP_Text textChannelName;
    [SerializeField] private TMP_Text textMembersCount;
    [SerializeField] private ChatProfileView chatProfileView;
    [SerializeField] private GameObject nearbyElementsContainer;

    private void Awake()
    {
        buttonClose.onClick.AddListener(() => OnCloseRequested?.Invoke());
        buttonOpenMembers.onClick.AddListener(() => OnMembersRequested?.Invoke());
    }

    public void Setup(ChatTitlebarViewModel model)
    {
        if (model.IsLoadingProfile)
        {
            textChannelName.text = model.Name;
            chatProfileView.gameObject.SetActive(false);
            nearbyElementsContainer.SetActive(false);
            buttonOpenMembers.gameObject.SetActive(false);
            return;
        }
        
        textChannelName.text = model.Name;
        
        bool isDirectMessage = model.ViewMode == Mode.DirectMessage;
        chatProfileView.gameObject.SetActive(isDirectMessage);
        nearbyElementsContainer.SetActive(!isDirectMessage);
        buttonOpenMembers.gameObject.SetActive(!isDirectMessage);

        if (isDirectMessage)
        {
            chatProfileView.Setup(model);
            chatProfileView.SetProfileBackgroundColor(model.ProfileColor);
        }
    }
    public void SetMemberCount(string count) => textMembersCount.text = count;
    public void Activate(bool activate) => gameObject.SetActive(activate);
}
