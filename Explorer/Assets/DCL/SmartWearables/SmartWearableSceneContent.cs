using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using SceneRunner.Scene;
using System;

namespace DCL.SmartWearables
{
    /// <summary>
    /// Provides content for Smart Wearable scenes.
    /// </summary>
    /// <remarks>
    /// Smart Wearable scenes contain assets for both MALE and FEMALE shapes, in the format 'male/my-asset.xyz' or 'female/my-asset.xyz'.
    /// Scenes normally request assets by their name without prefixing them, so this class automatically adds male/ or female/ prefixes (depending on the
    /// body shape of the player avatar).
    ///
    /// For example, if the avatar's body shape is MALE:
    /// If a scene requests 'my-obj.glb', this class will first try to resolve 'my-obj.glb', if that fails, it will try 'male/my-obj.glb'.
    /// </remarks>
    public class SmartWearableSceneContent : ISceneContent
    {
        private ISceneContent content;

        private string contentPrefix;

        private SmartWearableSceneContent(URLDomain contentBaseUrl, IWearable wearable, string contentPrefix)
        {
            ContentBaseUrl = contentBaseUrl;
            this.content = new SceneHashedContent(wearable.DTO.content, contentBaseUrl);
            this.contentPrefix = contentPrefix;
        }

        public URLDomain ContentBaseUrl { get;}

        public bool TryGetContentUrl(string contentPath, out URLAddress result) =>
            content.TryGetContentUrl(contentPrefix + contentPath, out result) || content.TryGetContentUrl(contentPath, out result);

        public bool TryGetHash(string name, out string hash) =>
            content.TryGetHash(contentPrefix + name, out hash) || content.TryGetHash(name, out hash);

        public static SmartWearableSceneContent Create(URLDomain contentBaseUrl, IWearable wearable, BodyShape bodyShape) =>
            new (contentBaseUrl, wearable, GetContentPrefix(bodyShape));

        private static string GetContentPrefix(BodyShape bodyShape)
        {
            if (bodyShape.Index == BodyShape.MALE.Index) return "male/";
            if (bodyShape.Index == BodyShape.FEMALE.Index) return "female/";
            throw new ArgumentOutOfRangeException();
        }
    }
}
