using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using UnityEngine;

public class ChannelItemFactory
{
    private readonly ChatConfig config;
    private readonly ProfileRepositoryWrapper profileRepository;

    public ChannelItemFactory(ChatConfig config, ProfileRepositoryWrapper profileRepository)
    {
        this.config = config;
        this.profileRepository = profileRepository;
    }

    public async UniTask<ChatConversationsToolbarViewItem> Create(ChatChannel channel, Transform parent)
    {
        ChatConversationsToolbarViewItem newItem = Object.Instantiate(config.ItemPrefab, parent);
        newItem.Id = channel.Id;

        switch (channel.ChannelType)
        {
            case ChatChannel.ChatChannelType.NEARBY:
                newItem.SetConversationIcon(config.NearbyConversationIcon);
                newItem.SetConversationName(config.NearbyConversationName);
                newItem.SetClaimedNameIconVisibility(false);
                newItem.SetConversationType(false);
                break;

            case ChatChannel.ChatChannelType.USER:
                newItem.SetConversationName("Loading...");
                newItem.SetConversationType(true);
                
                var profile = await profileRepository.GetProfileAsync(channel.Id.Id, CancellationToken.None);
                if (profile != null)
                {
                    newItem.SetProfileData(profileRepository, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
                    newItem.SetConversationName(profile.ValidatedName);
                    newItem.SetClaimedNameIconVisibility(profile.HasClaimedName);
                }
                break;
                
            case ChatChannel.ChatChannelType.COMMUNITY:
                break;
        }

        newItem.Initialize();
        return newItem;
    }
}