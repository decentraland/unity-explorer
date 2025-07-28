using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.Services;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat.EventBus;
using DCL.UI.ProfileElements;
using UnityEngine;

using Utility;

namespace DCL.Chat.ChatUseCases
{
    public class GetChannelMembersCommand
    {
        private readonly IEventBus eventBus;
        private readonly ChatMemberListService memberListService;
        private readonly ChatConfig chatConfig;

        public GetChannelMembersCommand(
            IEventBus eventBus,
            ChatMemberListService memberListService,
            ChatConfig chatConfig)
        {
            this.eventBus = eventBus;
            this.memberListService = memberListService;
            this.chatConfig = chatConfig;
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

                GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.ProfileThumbnail, chatConfig.DefaultProfileThumbnail,
                                               viewModel.UserId, member.FaceSnapshotUrl, ct)
                                          .Forget();
            }

            targetList.Sort(static (a, b)
                => string.Compare(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
