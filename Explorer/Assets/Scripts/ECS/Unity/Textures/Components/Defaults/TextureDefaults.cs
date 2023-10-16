using CommunicationData.URLHelpers;
using Decentraland.Common;
using JetBrains.Annotations;
using SceneRunner.Scene;
using UnityEngine;
using Texture = Decentraland.Common.Texture;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace ECS.Unity.Textures.Components.Defaults
{
    public static class TextureDefaults
    {
        public static TextureComponent? CreateTextureComponent([CanBeNull] this TextureUnion self, ISceneData data)
        {
            if (self == null)
                return null;

            if (!self.TryGetTextureUrl(data, out URLAddress url))
                return null;

            return new TextureComponent(url, self.GetWrapMode(), self.GetFilterMode(), self.IsVideoTexture());
        }

        public static bool TryGetTextureUrl(this TextureUnion self, ISceneData data, out URLAddress url)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    return self.AvatarTexture.TryGetTextureUrl(out url);
                case TextureUnion.TexOneofCase.VideoTexture:
                    // Not implemented - just ignore to not break the loop
                    url = URLAddress.EMPTY;
                    return false;
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.TryGetTextureUrl(data, out url);
            }
        }

        public static long GetVideoTextureId(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.VideoTexture:
                    return self.VideoTexture.VideoPlayerEntity;
                default:
                    return 0;
            }
        }

        public static bool IsVideoTexture(this TextureUnion self) =>
            self.TexCase == TextureUnion.TexOneofCase.VideoTexture;

        public static TextureWrapMode GetWrapMode(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    return self.AvatarTexture.GetWrapMode();
                case TextureUnion.TexOneofCase.VideoTexture:
                    return self.VideoTexture.GetWrapMode();
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.GetWrapMode();
            }
        }

        public static FilterMode GetFilterMode(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    return self.AvatarTexture.GetFilterMode();
                case TextureUnion.TexOneofCase.VideoTexture:
                    return self.VideoTexture.GetFilterMode();
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.GetFilterMode();
            }
        }

        public static bool TryGetTextureUrl(this Texture self, ISceneData data, out URLAddress url) =>
            data.TryGetMediaUrl(self.Src, out url);

        public static bool TryGetTextureUrl(this AvatarTexture self, out URLAddress url)
        {
            // Not implemented
            url = URLAddress.EMPTY;
            return false;
        }

        public static TextureWrapMode GetWrapMode(this Texture self) =>
            self.HasWrapMode ? self.WrapMode.ToUnityWrapMode() : TextureWrapMode.Clamp;

        public static TextureWrapMode GetWrapMode(this AvatarTexture self) =>
            self.HasWrapMode ? self.WrapMode.ToUnityWrapMode() : TextureWrapMode.Clamp;

        public static TextureWrapMode GetWrapMode(this VideoTexture self) =>
            self.HasWrapMode ? self.WrapMode.ToUnityWrapMode() : TextureWrapMode.Clamp;

        public static FilterMode GetFilterMode(this Texture self) =>
            (self.HasFilterMode ? self.FilterMode : TextureFilterMode.TfmBilinear).ToUnityFilterMode();

        public static FilterMode GetFilterMode(this AvatarTexture self) =>
            (self.HasFilterMode ? self.FilterMode : TextureFilterMode.TfmBilinear).ToUnityFilterMode();

        public static FilterMode GetFilterMode(this VideoTexture self) =>
            (self.HasFilterMode ? self.FilterMode : TextureFilterMode.TfmBilinear).ToUnityFilterMode();

        public static FilterMode ToUnityFilterMode(this TextureFilterMode self) =>
            (FilterMode)self;

        public static TextureWrapMode ToUnityWrapMode(this Decentraland.Common.TextureWrapMode self) =>
            (TextureWrapMode)self;
    }
}
