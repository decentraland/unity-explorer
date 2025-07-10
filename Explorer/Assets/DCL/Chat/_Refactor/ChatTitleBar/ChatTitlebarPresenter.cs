using System;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DG.Tweening;
using UnityEngine;

public class ChatTitlebarPresenter : IDisposable
{
    private readonly IChatTitlebarView view;
    private readonly IRoomHub roomHub;
    private readonly IProfileCache profileCache;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

    public event Action? OnCloseChat;
    public event Action? OnOpenMembers;
    public event Action? OnOpenUserContextMenu;
    public event Action? OnOpenContextMenu;
    public event Action<bool>? OnMemberListToggle;

    public ChatTitlebarPresenter(
        IChatTitlebarView view,
        IRoomHub roomHub,
        IProfileCache profileCache,
        ProfileRepositoryWrapper profileRepositoryWrapper)
    {
        this.view = view;
        view.Initialize();
        this.roomHub = roomHub;
        this.profileCache = profileCache;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
    }

    public void Enable()
    {
        // view.OnCloseClicked += OnCloseButtonClicked;
        view.CloseChatButtonClicked += OnCloseButtonClicked;
        view.CloseMemberListButtonClicked += OnCloseButtonClicked;
        view.OnMemberListToggled += OnMemberListToggled;
    }
    
    public void Show()
    {
        view.Show();
    }
    
    public void Hide()
    {
        view.Hide();
    }

    private void OnMemberListToggled(bool active)
    {
        OnMemberListToggle?.Invoke(active);
    }

    private void OnCloseButtonClicked()
    {
        OnCloseChat?.Invoke();
    }

    public void UpdateForChannel(ChatChannel channel)
    {
        view.SetChannelNameText(channel.Id.ToString());
        view.SetupProfileView(new Web3Address(channel.Id.Id), profileRepositoryWrapper);
    }

    public void Dispose()
    {
        view.OnCloseClicked -= OnCloseButtonClicked;
        view.OnMemberListToggled -= OnMemberListToggled;
    }

    public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
    {
        view.SetFocusedState(isFocused, animate, duration, easing);
    }
}