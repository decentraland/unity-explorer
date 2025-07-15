using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.EventBus;
using DCL.Chat.Services;
using DCL.UI.Profiles.Helpers;
using Utilities;

public class ChatMemberListPresenter : IDisposable
{
    private readonly IChatMemberListView view;
    private readonly IEventBus eventBus;
    private readonly ChatMemberListService memberListService;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
    private readonly EventSubscriptionScope scope = new EventSubscriptionScope();

    public ChatMemberListPresenter(
        IChatMemberListView view,
        IEventBus eventBus,
        ChatMemberListService memberListService,
        ProfileRepositoryWrapper profileRepositoryWrapper)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.memberListService = memberListService;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
        
        memberListService.OnMemberListUpdated += OnMemberListUpdated;
        memberListService.OnMemberCountUpdated += OnMemberCountUpdated;
    }

    private void OnMemberListUpdated(IReadOnlyList<ChatMemberListView.MemberData> members)
    {
        view.SetData(members);
    }
    
    private void OnMemberCountUpdated(int memberCount)
    {
        view.SetMemberCount(memberCount);
    }
    
    public void ShowAndLoadCurrentList()
    {
        view.SetData(memberListService.LastKnownMemberList);

        view.Show();

        memberListService.RequestRefreshAsync().Forget();
    }

    public void Show()
    {
        view.Show();
    }
    public void Hide()
    {
        view.Hide();
    }
    
    public void Dispose()
    {
        memberListService.OnMemberListUpdated -= OnMemberListUpdated;
        scope.Dispose();
    }
}