using System;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

public class ChatTitlebarPresenter : IDisposable
{
    private readonly IChatTitlebarView view;
    private readonly IRoomHub roomHub;
    private readonly IProfileCache profileCache;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

    public event Action? OnClosed;
    public event Action<bool>? OnMemberListToggle;

    public ChatTitlebarPresenter(
        IChatTitlebarView view,
        IRoomHub roomHub,
        IProfileCache profileCache,
        ProfileRepositoryWrapper profileRepositoryWrapper)
    {
        this.view = view;
        this.roomHub = roomHub;
        this.profileCache = profileCache;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
    }

    public void Enable()
    {
        view.OnCloseClicked += OnCloseButtonClicked;
        view.OnMemberListToggled += OnMemberListToggled;
    }

    private void OnMemberListToggled(bool active)
    {
        OnMemberListToggle?.Invoke(active);
    }

    private void OnCloseButtonClicked()
    {
        OnClosed?.Invoke();
    }

    public void UpdateForChannel(ChatChannel channel)
    {
    }

    public void Dispose()
    {
        view.OnCloseClicked -= OnCloseButtonClicked;
        view.OnMemberListToggled -= OnMemberListToggled;
    }
}