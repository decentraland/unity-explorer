using Arch.Core;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using Unity.Profiling;
using FontPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Fonts.FontData, ECS.StreamableLoading.Fonts.GetFontIntention>;

namespace ECS.StreamableLoading.Fonts
{
    public struct SceneFontRequest
    {
        private static readonly ProfilerMarker REQUEST_MARKER = new ($"{nameof(SceneFontRequest)}.Request");
        private static readonly ProfilerMarker RECEIVE_MARKER = new ($"{nameof(SceneFontRequest)}.Receive");

        public string? Src { get; private set; }

        public FontPromise? Promise { get; private set; }

        public bool Update(World world, ISceneData sceneData, string? fontSrc, IPartitionComponent partition)
        {
            fontSrc = string.IsNullOrWhiteSpace(fontSrc) ? null : fontSrc.Trim();

            if (string.Equals(fontSrc, Src, StringComparison.Ordinal))
                return false;

            Release(world);
            Src = fontSrc;

            if (fontSrc == null)
                return true;

            using ProfilerMarker.AutoScope _ = REQUEST_MARKER.Auto();

            if (FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out GetFontIntention intention))
                Promise = FontPromise.Create(world, intention, partition);

            return true;
        }

        public bool TryConsume(World world, out SceneFontAssets? assets)
        {
            assets = null;

            if (Promise == null || Promise.Value.IsConsumed)
                return false;

            FontPromise promise = Promise.Value;

            if (!promise.TryConsume(world, out StreamableLoadingResult<FontData> result))
                return false;

            Promise = promise;
            using ProfilerMarker.AutoScope _ = RECEIVE_MARKER.Auto();

            if (result.Succeeded)
                assets = result.Asset!.Asset;
            else
                result.TryLogException();

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
