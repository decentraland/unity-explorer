using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;

namespace DCL.PluginSystem
{
    public class SmartWearableSceneContent : ISceneContent
    {
        private Dictionary<string, string> hashTable = new ();

        private Dictionary<string, (bool, URLAddress)> cachedUrls = new();

        private SmartWearableSceneContent(URLDomain contentBaseUrl)
        {
            ContentBaseUrl = contentBaseUrl;
        }

        public URLDomain ContentBaseUrl { get;}

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            if (cachedUrls.TryGetValue(contentPath, out (bool success, URLAddress url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            if (hashTable.TryGetValue(contentPath, out string hash))
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

        public static SmartWearableSceneContent Create(URLDomain contentBaseUrl, IWearable wearable, BodyShape bodyShape)
        {
            var result = new SmartWearableSceneContent(contentBaseUrl);

            string contentPrefix = GetContentPrefix(bodyShape);
            foreach (var content in wearable.DTO.content)
            {
                string key = content.file;

                // NOTE we use StartsWith because the string female/ also contains male/
                if (content.file.StartsWith(contentPrefix))
                    // Remove the prefix so that we don't need it when querying content with TryGetContentUrl
                    key = content.file.Replace(contentPrefix, string.Empty);

                result.hashTable.Add(key, content.hash);
            }

            return result;
        }

        private static string GetContentPrefix(BodyShape bodyShape)
        {
            if (bodyShape.Index == BodyShape.MALE.Index) return "male/";

            if (bodyShape.Index == BodyShape.FEMALE.Index) return "female/";

            throw new ArgumentOutOfRangeException();
        }
    }
}
