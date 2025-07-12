using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Chat.Services;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

public class ChatMemberListPresenter : IDisposable
{
    private readonly IChatMemberListView view;
    private readonly ChatMemberListService memberListService;
    private readonly IRoomHub roomHub;
    private readonly IProfileCache profileCache;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

    public ChatMemberListPresenter(
        IChatMemberListView view,
        ChatMemberListService memberListService,
        IRoomHub roomHub,
        IProfileCache profileCache,
        ProfileRepositoryWrapper profileRepositoryWrapper)
    {
        this.view = view;
        this.memberListService = memberListService;
        this.roomHub = roomHub;
        this.profileCache = profileCache;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
    }

    public void Activate()
    {
        memberListService.OnMemberListUpdated += OnMemberListUpdated;
    }

    public void Deactivate()
    {
        memberListService.OnMemberListUpdated -= OnMemberListUpdated;
        view.Hide();
    }

    public void LoadMembersForChannel(ChatChannel channel)
    {
        
    }
    
    private void OnMemberListUpdated(IReadOnlyList<ChatMemberListView.MemberData> members)
    {
        // React to live updates from the service
        view.SetData(members);
    }
    
    public void ShowAndLoadCurrentList()
    {
        // Immediately display the last known data (the "snapshot")
        view.SetData(memberListService.LastKnownMemberList);

        // Show the view
        view.Show();

        // (Optional but recommended) Ask the service to double-check for fresh data in the background.
        // If there are changes, the OnMemberListUpdated event will fire and update the view.
        memberListService.RequestRefreshAsync().Forget();
    }

    public void Dispose()
    {
        // Clean up any background tasks or event subscriptions
    }
    
    public void Show()
    {
        view.Show();
    }
    
    public void Hide()
    {
        view.Hide();
    }
}