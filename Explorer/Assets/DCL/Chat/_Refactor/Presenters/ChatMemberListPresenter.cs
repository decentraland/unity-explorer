using System;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

public class ChatMemberListPresenter : IDisposable
{
    private readonly IChatMemberListView view;
    private readonly IRoomHub roomHub;
    private readonly IProfileCache profileCache;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

    public ChatMemberListPresenter(
        IChatMemberListView view,
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
    }

    public void Disable()
    {
    }

    public void LoadMembersForChannel(ChatChannel channel)
    {
        // Logic to get participants from roomHub and update the view
        // Only does the update if isEnabled is true
    }

    public void Dispose()
    {
        // Clean up any background tasks or event subscriptions
    }
}