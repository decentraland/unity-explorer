using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.Services;
using Utilities;
using Utility;

public class ChatMemberListPresenter : IDisposable
{
    private readonly ChannelMemberFeedView view;
    private readonly IEventBus eventBus;
    private readonly GetChannelMembersCommand getChannelMembersCommand;
    private readonly ChatMemberListService memberListService;
    private readonly EventSubscriptionScope scope = new ();
    private CancellationTokenSource cts = new ();

    private List<ChatMemberListViewModel> currentMembers = new ();
    
    public ChatMemberListPresenter(
        ChannelMemberFeedView view,
        IEventBus eventBus,
        ChatMemberListService memberListService,
        GetChannelMembersCommand getChannelMembersCommand)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.memberListService = memberListService;
        this.getChannelMembersCommand = getChannelMembersCommand;
        
        this.view.OnMemberContextMenuRequested += OnMemberContextMenuRequested;
        scope.Add(eventBus.Subscribe<ChatEvents.ChannelMemberUpdatedEvent>(OnMemberUpdated));
    }

    public void ShowAndLoad()
    {
        view.Show();
        cts = new CancellationTokenSource();
        memberListService.OnMemberListUpdated += HandleLiveUpdate;
        HandleLiveUpdate(memberListService.LastKnownMemberList);
    }

    private void OnMemberUpdated(ChatEvents.ChannelMemberUpdatedEvent evt)
    {
        int index = currentMembers.FindIndex(m => m.UserId == evt.ViewModel.UserId);
        if (index != -1)
        {
            currentMembers[index] = evt.ViewModel;
            view.UpdateMember(evt.ViewModel);
        }
    }

    private void HandleLiveUpdate(IReadOnlyList<ChatMemberListView.MemberData> members)
    {
        cts.Cancel();
        cts = new CancellationTokenSource();

        // Get the list of initial view models from the command.
        // The command will handle starting the thumbnail downloads.
        currentMembers = getChannelMembersCommand.GetInitialMembersAndStartLoadingThumbnails(cts.Token);

        // Immediately display this list. Names appear instantly, pictures are loading.
        view.SetData(currentMembers);
    }
    
    public void Show() => ShowAndLoad();

    public void Hide()
    {
        view.Hide();
        memberListService.OnMemberListUpdated -= HandleLiveUpdate;

        currentMembers.Clear();
        view.SetData(currentMembers);

        cts.Cancel();
    }

    private void OnMemberContextMenuRequested(string obj)
    {
        
    }
    
    public void Dispose()
    {
        if (memberListService != null)
            memberListService.OnMemberListUpdated -= HandleLiveUpdate;
        
        cts.SafeCancelAndDispose();
        scope.Dispose();
    }
}