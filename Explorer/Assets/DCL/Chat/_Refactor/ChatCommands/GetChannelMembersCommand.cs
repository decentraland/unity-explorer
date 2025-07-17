using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.Services;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat.EventBus;
using UnityEngine;
using Utilities;

namespace DCL.Chat.ChatUseCases
{
    public class GetChannelMembersCommand
    {
        private readonly IEventBus eventBus;
        private readonly ChatMemberListService memberListService;
        private readonly GetProfileThumbnailCommand getProfileThumbnailCommand;

        public GetChannelMembersCommand(IEventBus eventBus,
            ChatMemberListService memberListService,
            GetProfileThumbnailCommand getProfileThumbnailCommand)
        {
            this.eventBus = eventBus;
            this.memberListService = memberListService;
            this.getProfileThumbnailCommand = getProfileThumbnailCommand;
        }

        public List<ChatMemberListViewModel> GetInitialMembersAndStartLoadingThumbnails(
            IReadOnlyList<ChatMemberListView.MemberData> rawMembers, CancellationToken ct)
        {
            var viewModels = new List<ChatMemberListViewModel>(rawMembers.Count);

            foreach (var member in rawMembers)
            {
                var viewModel = new ChatMemberListViewModel
                {
                    UserId = member.Id, WalletId = member.WalletId, UserName = member.Name, ProfilePicture = null,
                    IsOnline = member.ConnectionStatus == ChatMemberConnectionStatus.Online,
                    ProfileColor = member.ProfileColor, HasClaimedName = member.HasClaimedName, IsLoading = true
                };

                viewModels.Add(viewModel);

                FetchThumbnailAndUpdateAsync(viewModel, member.FaceSnapshotUrl, ct).Forget();
            }

            viewModels.Sort((a, b) 
                => string.Compare(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase));

            return viewModels;
        }

        private async UniTaskVoid FetchThumbnailAndUpdateAsync(ChatMemberListViewModel viewModel, string faceSnapshotUrl, CancellationToken ct)
        {
            var thumbnail = await getProfileThumbnailCommand.ExecuteAsync(viewModel.UserId, faceSnapshotUrl, ct);

            if (ct.IsCancellationRequested) return;

            viewModel.ProfilePicture = thumbnail;
            viewModel.IsLoading = false;

            eventBus.Publish(new ChatEvents.ChannelMemberUpdatedEvent
            {
                ViewModel = viewModel
            });
        }
    }
}