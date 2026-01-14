using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.UI.ProfileElements;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    public class GetChannelMembersCommand
    {
        private readonly ChatConfig.ChatConfig chatConfig;

        public GetChannelMembersCommand(ChatConfig.ChatConfig chatConfig)
        {
            this.chatConfig = chatConfig;
        }

        public void GetInitialMembersAndStartLoadingThumbnails(
            IReadOnlyList<ChatMemberListData> rawMembers,
            List<ChatMemberListViewModel> targetList,
            CancellationToken ct)
        {
            targetList.Clear();

            foreach (var member in rawMembers)
            {
                var viewModel = new ChatMemberListViewModel(member.Profile, member.ConnectionStatus == ChatMemberConnectionStatus.Online);

                targetList.Add(viewModel);

                GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.ProfileThumbnail, chatConfig.DefaultProfileThumbnail, viewModel.Profile, ct)
                                          .Forget();
            }

            targetList.Sort(static (a, b)
                => string.Compare(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
