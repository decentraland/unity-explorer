using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;

namespace ECS.StreamableLoading.Fonts
{
    public static class FontSrcResolver
    {
        public static bool TryCreateIntention(string fontSrc, ISceneData sceneData, out GetFontIntention intention)
        {
            intention = default(GetFontIntention);

            if (Uri.TryCreate(fontSrc, UriKind.Absolute, out _)
                || fontSrc.StartsWith("//", StringComparison.Ordinal))
            {
                ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"font_src \"{fontSrc}\" cannot be an external URL");
                return false;
            }

            if (!sceneData.TryGetContentUrl(fontSrc, out URLAddress url))
            {
                ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"font_src \"{fontSrc}\" is not a content file of the scene {sceneData.SceneShortInfo}");
                return false;
            }

            intention = new GetFontIntention
            {
                Src = fontSrc,
                CommonArguments = new CommonLoadingArguments(url),
            };

            AssignAssetBundle(fontSrc, sceneData, ref intention);
            return true;
        }

        private static void AssignAssetBundle(string fontSrc, ISceneData sceneData, ref GetFontIntention intention)
        {
            if (sceneData.SceneEntityDefinition is not { } definition
                || !sceneData.TryGetHash(fontSrc, out string hash))
                return;

            AssetBundleManifestVersion manifest = definition.AssetBundleManifestVersionOrFailed;

            if (manifest.assetBundleManifestRequestFailed || manifest.IsLSDAsset)
                return;

            intention.AssetBundleHash = hash;
            intention.AssetBundleListed = manifest.ListsConvertedFile(hash);
            intention.AssetBundleManifest = manifest;
            intention.SceneId = definition.id ?? string.Empty;
        }
    }
}
