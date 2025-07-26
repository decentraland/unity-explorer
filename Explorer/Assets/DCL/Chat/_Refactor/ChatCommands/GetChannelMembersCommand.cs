using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.Services;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat.EventBus;
using UnityEngine;

using Utility;

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

        public void GetInitialMembersAndStartLoadingThumbnails(
            IReadOnlyList<ChatMemberListView.MemberData> rawMembers,
            List<ChatMemberListViewModel> targetList,
            CancellationToken ct)
        {
            targetList.Clear();

            foreach (var member in rawMembers)
            {
                var viewModel = new ChatMemberListViewModel(member.Id, member.WalletId, member.Name,
                    member.ConnectionStatus == ChatMemberConnectionStatus.Online, member.ProfileColor, member.HasClaimedName);

                targetList.Add(viewModel);

                FetchThumbnailAndUpdateAsync(viewModel, member.FaceSnapshotUrl, ct).Forget();
            }

            targetList.Sort(static (a, b)
                => string.Compare(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase));
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
