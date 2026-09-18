using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;

namespace ECS.StreamableLoading.Fonts
{
    public static class FontSrcResolver
    {
        private const int ATTEMPTS_COUNT = 2;

        public static bool TryCreateIntention(string fontSrc, ISceneData sceneData, out GetFontIntention intention)
        {
            intention = default(GetFontIntention);

            if (Uri.TryCreate(fontSrc, UriKind.Absolute, out _)
                || fontSrc.StartsWith("//", StringComparison.Ordinal))
            {
                ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"font_src \"{fontSrc}\" cannot be an external URL");
                return false;
            }

            FontSourceKind kind = FontSourceKind.File;

            if (!sceneData.TryGetContentUrl(fontSrc, out URLAddress url))
            {
                string? familyId = FontsourceCatalog.ToFamilyId(fontSrc);

                if (familyId == null)
                {
                    ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"font_src \"{fontSrc}\" is neither a content file of the scene {sceneData.SceneShortInfo} nor a font family name");
                    return false;
                }

                kind = FontSourceKind.FontsourceFamily;
                url = URLAddress.FromString(FontsourceCatalog.ApiUrl(familyId));
            }

            intention = new GetFontIntention
            {
                Kind = kind,
                Src = fontSrc,
                CommonArguments = new CommonLoadingArguments(url, attempts: ATTEMPTS_COUNT),
            };

            return true;
        }
    }
}
