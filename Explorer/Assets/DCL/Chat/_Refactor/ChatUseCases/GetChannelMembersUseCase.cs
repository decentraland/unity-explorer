using System;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.Services;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.ChatUseCases
{
    public class GetChannelMembersUseCase
    {
        private readonly ChatMemberListService memberListService;
        private readonly GetProfileThumbnailUseCase getProfileThumbnailUseCase;

        public GetChannelMembersUseCase( ChatMemberListService memberListService,
            GetProfileThumbnailUseCase getProfileThumbnailUseCase)
        {
            this.memberListService = memberListService;
            this.getProfileThumbnailUseCase = getProfileThumbnailUseCase;
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
            Sprite thumbnail = await getProfileThumbnailUseCase.ExecuteAsync(member.Id, member.FaceSnapshotUrl, ct);

            if (ct.IsCancellationRequested) return null; // Handle cancellation

            return new ChatMemberListViewModel
            {
                UserId = member.Id, UserName = member.Name, ProfilePicture = thumbnail, IsOnline = member.ConnectionStatus == ChatMemberConnectionStatus.Online,
                ProfileColor = member.ProfileColor
            };
        }
    }
}