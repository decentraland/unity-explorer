using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    public interface IIpfsRealm
    {
        public URLDomain EntitiesBaseUrl { get; }

        public URLDomain CatalystBaseUrl { get; }

        /// <summary>
        ///     This URL should be used directly only in specific cases, generally it's advised to use <see cref="IDecentralandUrlsSource.Url(DecentralandUrl.Content)" /> instead to avoid possible desynchronizations
        /// </summary>
        public URLDomain ContentBaseUrl { get; }

        /// <summary>
        ///     This URL should be used directly only in specific cases, generally it's advised to use <see cref="IDecentralandUrlsSource.Url(DecentralandUrl.Lambdas)" /> instead to avoid possible desynchronizations
        /// </summary>
        public URLDomain LambdasBaseUrl { get; }

        public IReadOnlyList<string> SceneUrns { get; }

        public URLDomain EntitiesActiveEndpoint { get; }

        public string GetFileHash(byte[] file);
    }
}
