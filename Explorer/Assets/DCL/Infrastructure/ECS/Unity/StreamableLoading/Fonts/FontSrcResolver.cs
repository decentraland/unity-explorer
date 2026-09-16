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
                || fontSrc.StartsWith("//", StringComparison.Ordinal)
                || !sceneData.TryGetContentUrl(fontSrc, out URLAddress url))
            {
                ReportHub.LogWarning(ReportCategory.FONTS, $"font_src \"{fontSrc}\" is not a content file of the scene {sceneData.SceneShortInfo}");
                return false;
            }

            intention = new GetFontIntention
            {
                Src = fontSrc,
                CommonArguments = new CommonLoadingArguments(url, attempts: ATTEMPTS_COUNT),
            };

            return true;
        }
    }
}
