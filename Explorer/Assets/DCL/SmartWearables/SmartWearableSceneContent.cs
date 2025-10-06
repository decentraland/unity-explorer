using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.SmartWearables
{
    public class SmartWearableSceneContent : ISceneContent
    {
        private readonly Dictionary<string, (bool, URLAddress)> cachedUrls = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, string> content = new (StringComparer.OrdinalIgnoreCase);

        private string contentPrefix;

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

            if (TryGetHash(contentPath, out string hash))
            {
                result = ContentBaseUrl.Append(URLPath.FromString(hash));
                cachedUrls[contentPath] = (true, result);
                return true;
            }

            result = URLAddress.EMPTY;
            cachedUrls[contentPath] = (false, result);
            return false;
        }

        public bool TryGetHash(string name, out string hash) =>
            content.TryGetValue(name, out hash) || content.TryGetValue(contentPrefix + name, out hash);

        public static SmartWearableSceneContent Create(URLDomain contentBaseUrl, IWearable wearable, BodyShape bodyShape)
        {
            return new SmartWearableSceneContent(contentBaseUrl)
            {
                contentPrefix = GetContentPrefix(bodyShape),
                content = wearable.DTO.content.ToDictionary(x => x.file, x => x.hash),
            };
        }

        private static string GetContentPrefix(BodyShape bodyShape)
        {
            if (bodyShape.Index == BodyShape.MALE.Index) return "male/";
            if (bodyShape.Index == BodyShape.FEMALE.Index) return "female/";
            throw new ArgumentOutOfRangeException();
        }
    }
}
