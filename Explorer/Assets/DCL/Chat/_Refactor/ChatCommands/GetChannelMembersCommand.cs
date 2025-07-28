using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat.EventBus;
using DCL.UI.ProfileElements;
using UnityEngine;

namespace DCL.Chat.ChatUseCases
{
    public class GetChannelMembersCommand
    {
        private readonly ChatConfig chatConfig;

        public GetChannelMembersCommand(ChatConfig chatConfig)
        {
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
