using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.Services;
using DCL.Web3;
using MVC;
using UnityEngine;
using Utilities;
using Utility;

public class ChatMemberListPresenter : IDisposable
{
    private readonly ChannelMemberFeedView view;
    private readonly IEventBus eventBus;
    private readonly GetChannelMembersCommand getChannelMembersCommand;
    private readonly ChatMemberListService memberListService;
    private readonly ChatContextMenuService chatContextMenuService;
    
    private readonly EventSubscriptionScope scope = new ();
    private CancellationTokenSource lifeCts = new ();
    private CancellationTokenSource? activeMenuCts;
    private UniTaskCompletionSource? activeMenuTcs;

    private List<ChatMemberListViewModel> currentMembers = new ();
    
    public ChatMemberListPresenter(
        ChannelMemberFeedView view,
        IEventBus eventBus,
        ChatMemberListService memberListService,
        ChatContextMenuService chatContextMenuService,
        GetChannelMembersCommand getChannelMembersCommand)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.memberListService = memberListService;
        this.getChannelMembersCommand = getChannelMembersCommand;
        this.chatContextMenuService = chatContextMenuService;
        
        this.view.OnMemberContextMenuRequested += OnMemberContextMenuRequested;
        scope.Add(eventBus.Subscribe<ChatEvents.ChannelMemberUpdatedEvent>(OnMemberUpdated));
    }

    public void ShowAndLoad()
    {
        view.Show();
        lifeCts = new CancellationTokenSource();
        memberListService.OnMemberListUpdated += HandleLiveUpdate;
        memberListService.RequestRefreshAsync().Forget();
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

    private void HandleLiveUpdate(IReadOnlyList<ChatMemberListView.MemberData> freshMembers)
    {
        lifeCts.Cancel();
        lifeCts = new CancellationTokenSource();

        // Get the list of initial view models from the command.
        // The command will handle starting the thumbnail downloads.
        currentMembers = getChannelMembersCommand
            .GetInitialMembersAndStartLoadingThumbnails(freshMembers, lifeCts.Token);

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

        lifeCts.Cancel();
    }

    private void OnMemberContextMenuRequested(UserProfileMenuRequest data)
    {
        chatContextMenuService
            .ShowUserProfileMenuAsync(data).Forget();
    }
    
    public void Dispose()
    {
        if (memberListService != null)
            memberListService.OnMemberListUpdated -= HandleLiveUpdate;

        lifeCts.SafeCancelAndDispose();
        scope.Dispose();
    }
}