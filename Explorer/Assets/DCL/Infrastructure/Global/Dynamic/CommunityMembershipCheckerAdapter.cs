using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.PrivateWorlds;
using System.Threading;

namespace Global.Dynamic
{
    /// <summary>
    /// Implements <see cref="ICommunityMembershipChecker"/> using <see cref="CommunitiesDataProvider"/>.
    /// Lives in DCL.Plugins to avoid DCL.PrivateWorlds referencing DCL.Social (cyclic dependency).
    /// </summary>
    public class CommunityMembershipCheckerAdapter : ICommunityMembershipChecker
    {
        private readonly CommunitiesDataProvider communitiesDataProvider;

        public CommunityMembershipCheckerAdapter(CommunitiesDataProvider communitiesDataProvider)
        {
            this.communitiesDataProvider = communitiesDataProvider;
        }

        public async UniTask<bool> IsMemberOfCommunityAsync(string communityId, CancellationToken ct)
        {
            var response = await communitiesDataProvider.GetCommunityAsync(communityId, ct);
            return response.data.role != CommunityMemberRole.none;
        }
    }
}
