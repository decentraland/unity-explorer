using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using ECS;
using Ipfs;
using System;
using System.Threading;

namespace DCL.Profiles
{
    public class RealmProfileRepository : IProfileRepository
    {
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly URLBuilder urlBuilder = new ();

        public RealmProfileRepository(IWebRequestController webRequestController,
            IRealmData realm)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
        }

        public async UniTask<Profile?> Get(string id, int version, CancellationToken ct)
        {
            IIpfsRealm ipfs = realm.Ipfs;

            urlBuilder.Clear();

            urlBuilder.AppendDomain(ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"))
                      .AppendParameter(new URLParameter("version", version.ToString()));

            URLAddress url = urlBuilder.Build();

            try
            {
                GenericGetRequest response = await webRequestController.GetAsync(new CommonArguments(url, timeout: 30), ct);

                GetProfileJsonRootDto root = await response.CreateFromJson<GetProfileJsonRootDto>(WRJsonParser.Unity,
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, version, exception));

                return root.avatars.Length == 0 ? null : root.avatars[0].ToProfile();
            }
            catch (UnityWebRequestException) { return null; }
        }

        [Serializable]
        public class GetProfileJsonRootDto
        {
            public long timestamp;
            public ProfileJsonDto[] avatars;
        }
    }
}
