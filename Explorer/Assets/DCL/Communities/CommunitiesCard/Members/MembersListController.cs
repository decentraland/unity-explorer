using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListController : IDisposable
    {
        private readonly MembersListView view;

        private string lastCommunityId = string.Empty;

        public MembersListController(MembersListView view)
        {
            this.view = view;
        }

        public void Dispose()
        {
        }

        public void Reset()
        {
            lastCommunityId = string.Empty;
        }

        public async UniTaskVoid ShowMembersListAsync(string communityId, CancellationToken ct)
        {
            if (lastCommunityId.Equals(communityId)) return;

            lastCommunityId = communityId;
        }
    }
}
