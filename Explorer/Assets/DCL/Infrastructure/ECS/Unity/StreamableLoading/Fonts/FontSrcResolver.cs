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

            if (LooksLikeFileReference(fontSrc))
            {
                if (!sceneData.TryGetMediaUrl(fontSrc, out URLAddress url))
                {
                    ReportHub.LogWarning(ReportCategory.FONTS, $"font_src \"{fontSrc}\" is neither a content file of the scene {sceneData.SceneShortInfo} nor an allowed media URL, the built-in font stays");
                    return false;
                }

                intention = new GetFontIntention
                {
                    Kind = FontSourceKind.File,
                    Src = fontSrc,
                    CommonArguments = new CommonLoadingArguments(url, attempts: ATTEMPTS_COUNT),
                };

                return true;
            }

            string? familyId = FontsourceCatalog.ToFamilyId(fontSrc);

            if (familyId == null)
            {
                ReportHub.LogWarning(ReportCategory.FONTS, $"font_src \"{fontSrc}\" is neither a scene file, a URL nor a font family name, the built-in font stays");
                return false;
            }

            intention = new GetFontIntention
            {
                Kind = FontSourceKind.FontsourceFamily,
                Src = fontSrc,
                CommonArguments = new CommonLoadingArguments(URLAddress.FromString(FontsourceCatalog.ApiUrl(familyId)), attempts: ATTEMPTS_COUNT),
            };

            return true;
        }

        private static bool LooksLikeFileReference(string value) =>
            value.IndexOf('/') >= 0
            || value.IndexOf('\\') >= 0
            || value.EndsWith(FontFileStore.TRUE_TYPE_EXTENSION, StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(FontFileStore.OPEN_TYPE_EXTENSION, StringComparison.OrdinalIgnoreCase);
    }
}
