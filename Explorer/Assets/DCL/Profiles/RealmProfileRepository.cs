using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using ECS;
using Ipfs;
using System.Threading;

namespace DCL.Profiles
{
    public class RealmProfileRepository : IProfileRepository
    {
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly CacheProfileRepository cacheProfileRepository;
        private readonly URLBuilder urlBuilder = new ();

        public RealmProfileRepository(IWebRequestController webRequestController,
            IRealmData realm,
            CacheProfileRepository cacheProfileRepository)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.cacheProfileRepository = cacheProfileRepository;
        }

        public async UniTask<Profile?> GetAsync(string id, int version, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id)) return null;

            IIpfsRealm ipfs = realm.Ipfs;

            urlBuilder.Clear();

            urlBuilder.AppendDomain(ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"))
                      .AppendParameter(new URLParameter("version", version.ToString()));

            URLAddress url = urlBuilder.Build();

            try
            {
                GenericGetRequest response = await webRequestController.GetAsync(new CommonArguments(url), ct);

                using var root = GetProfileJsonRootDto.Create();

                await response.OverwriteFromJsonAsync(root, WRJsonParser.Unity,
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, version, text, exception));

                if (root.avatars.Length == 0) return null;

                // TODO: probable responsibility issues thus we might not want to affect the local cache
                // but avoids extra allocations in case the profile already exists
                Profile profile = await cacheProfileRepository.GetAsync(id, version, ct) ?? new Profile();
                root.avatars[0].CopyTo(profile);
                cacheProfileRepository.Set(id, profile);

                return profile;
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode == 404)
                    return null;

                throw;
            }
        }
    }
}
