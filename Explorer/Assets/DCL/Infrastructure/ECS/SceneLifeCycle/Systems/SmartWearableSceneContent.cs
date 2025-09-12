using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;

namespace DCL.PluginSystem
{
    public class SmartWearableSceneContent : ISceneContent
    {
        private Dictionary<string, string> hashTable;

        private Dictionary<string, (bool, URLAddress)> cachedUrls = new();

        public SmartWearableSceneContent(URLDomain contentBaseUrl, IWearable wearable)
        {
            ContentBaseUrl = contentBaseUrl;
            hashTable = wearable.DTO.content.ToDictionary(x => x.file, x => x.hash);
        }

        public URLDomain ContentBaseUrl { get; }

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            if (cachedUrls.TryGetValue(contentPath, out (bool success, URLAddress url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            if (hashTable.TryGetValue(contentPath, out string hash) || hashTable.TryGetValue("male/" + contentPath, out hash))
            {
                result = ContentBaseUrl.Append(URLPath.FromString(hash));
                cachedUrls[contentPath] = (true, result);
                return true;
            }

            result = URLAddress.EMPTY;
            cachedUrls[contentPath] = (false, result);
            return false;
        }

        public bool TryGetHash(string name, out string hash)
        {
            return hashTable.TryGetValue(name, out hash);
        }
    }
}
