using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesDataProvider
{
    public class PrivateCommunitiesDataProvider
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly string communitiesBaseUrl;

        public PrivateCommunitiesDataProvider(string communitiesBaseUrl, IWeb3IdentityCache web3IdentityCache)
        {
            this.communitiesBaseUrl = communitiesBaseUrl;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask<GetUserInviteRequestResponse> GetUserInviteRequestAsync(InviteRequestAction action, int pageNumber, int elementsPerPage, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
