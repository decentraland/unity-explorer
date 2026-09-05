using Cysharp.Threading.Tasks;
using DCL.Communities.CommunityCreation;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Web3;
using MVC;
using System.Threading;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class CreateCommunityCommand
    {
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly ISpriteCache spriteCache;
        private readonly IMVCManager mvcManager;

        public CreateCommunityCommand(ISelfProfile selfProfile, INftNamesProvider nftNamesProvider, IMVCManager mvcManager, ISpriteCache spriteCache)
        {
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;
            this.mvcManager = mvcManager;
            this.spriteCache = spriteCache;
        }

        public void Execute(CancellationToken ct)
        {
            CreateCommunityAsync().Forget();
            return;

            async UniTaskVoid CreateCommunityAsync()
            {
                var canCreate = false;
                Profile? ownProfile = await selfProfile.ProfileAsync(ct);

                if (ownProfile != null)
                {
                    INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(ownProfile.UserId), 1, 1, ct);
                    canCreate = names.TotalAmount > 0;
                }

                mvcManager.ShowAsync(
                               CommunityCreationEditionController.IssueCommand(new CommunityCreationEditionParameter(
                                   canCreateCommunities: canCreate,
                                   communityId: string.Empty,
                                   spriteCache)), ct)
                          .Forget();
            }
        }


    }
}
