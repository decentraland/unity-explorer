using Arch.Core;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using FontPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Fonts.FontData, ECS.StreamableLoading.Fonts.GetFontIntention>;

namespace ECS.StreamableLoading.Fonts
{
    public struct SceneFontRequest
    {
        public string? Src { get; private set; }

        public FontPromise? Promise { get; private set; }

        public bool Update(World world, ISceneData sceneData, string? fontSrc, IPartitionComponent partition)
        {
            fontSrc = string.IsNullOrWhiteSpace(fontSrc) ? null : fontSrc.Trim();

            if (string.Equals(fontSrc, Src, StringComparison.Ordinal))
                return false;

            Release(world);
            Src = fontSrc;

            if (fontSrc != null && FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out GetFontIntention intention))
                Promise = FontPromise.Create(world, intention, partition);

            return true;
        }

        public bool TryConsume(World world, out FontFamilyAssets? assets)
        {
            assets = null;

            if (Promise == null || Promise.Value.IsConsumed)
                return false;

            FontPromise promise = Promise.Value;

            if (!promise.TryConsume(world, out StreamableLoadingResult<FontData> result))
                return false;

            Promise = promise;

            if (result.Succeeded)
                assets = result.Asset!.Asset;

            return true;
        }

        public void Release(World world)
        {
            Src = null;

            if (Promise == null)
                return;

            FontPromise promise = Promise.Value;
            promise.TryDereference(world);
            promise.ForgetLoading(world);
            Promise = null;
        }
    }
}
