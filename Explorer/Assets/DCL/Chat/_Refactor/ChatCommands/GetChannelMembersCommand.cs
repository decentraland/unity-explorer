using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.Services;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.ChatUseCases
{
    public class GetChannelMembersCommand
    {
        private readonly ChatMemberListService memberListService;
        private readonly GetProfileThumbnailCommand _getProfileThumbnailCommand;

        public GetChannelMembersCommand( ChatMemberListService memberListService,
            GetProfileThumbnailCommand getProfileThumbnailCommand)
        {
            this.memberListService = memberListService;
            this._getProfileThumbnailCommand = getProfileThumbnailCommand;
        }
        
        public async UniTask<List<ChatMemberListViewModel>> ExecuteAsync(CancellationToken ct)
        {
            IReadOnlyList<ChatMemberListView.MemberData> rawMembers = memberListService.LastKnownMemberList;
            var viewModels = new List<ChatMemberListViewModel>(rawMembers.Count);
            var tasks = new List<UniTask<ChatMemberListViewModel>>(rawMembers.Count);

            foreach (var member in rawMembers)
            {
                tasks.Add(CreateViewModelAsync(member, ct));
            }

            var results = await UniTask.WhenAll(tasks);

            viewModels.AddRange(results);

            viewModels.Sort((a, b) 
                => string.Compare(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase));

            return viewModels;
        }

        private async UniTask<ChatMemberListViewModel> CreateViewModelAsync(ChatMemberListView.MemberData member, CancellationToken ct)
        {
            Sprite thumbnail = await _getProfileThumbnailCommand.ExecuteAsync(member.Id, member.FaceSnapshotUrl, ct);

            if (ct.IsCancellationRequested) return null; // Handle cancellation

            return new ChatMemberListViewModel
            {
                UserId = member.Id, UserName = member.Name, ProfilePicture = thumbnail, IsOnline = member.ConnectionStatus == ChatMemberConnectionStatus.Online,
                ProfileColor = member.ProfileColor
            };
        }
    }
}