using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.UI.ProfileElements;
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

            // Ordering is owned by the service
            foreach (ChatMemberListData member in rawMembers)
            {
                var viewModel = new ChatMemberListViewModel(member.Profile, member.ConnectionStatus == ChatMemberConnectionStatus.Online, member.HasProfile);

                targetList.Add(viewModel);

                // A wallet placeholder has no picture to download
                if (!member.HasProfile)
                {
                    viewModel.ProfileThumbnail.UpdateValue(ProfileThumbnailViewModel.FromFallback(chatConfig.DefaultProfileThumbnail, viewModel.Profile.UserNameColor));

                    continue;
                }

                GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.ProfileThumbnail, chatConfig.DefaultProfileThumbnail, viewModel.Profile, ct)
                                          .Forget();
            }
        }
    }
}
