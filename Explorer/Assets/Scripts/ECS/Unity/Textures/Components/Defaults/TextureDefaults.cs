using Decentraland.Common;
using JetBrains.Annotations;
using SceneRunner.PublicAPI;
using System;
using UnityEngine;
using Texture = Decentraland.Common.Texture;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace ECS.Unity.Textures.Components.Defaults
{
    public static class TextureDefaults
    {
        public static TextureComponent? CreateTextureComponent([CanBeNull] this TextureUnion self, ISceneContentProvider contentProvider)
        {
            if (self == null)
                return null;

            if (!self.TryGetTextureUrl(contentProvider, out string url))
                return null;

            return new TextureComponent(url, self.GetWrapMode(), self.GetFilterMode(), self.IsVideoTexture());
        }

        public static bool TryGetTextureUrl(this TextureUnion self, ISceneContentProvider contentProvider, out string url)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    return self.AvatarTexture.TryGetTextureUrl(out url);
                case TextureUnion.TexOneofCase.VideoTexture:
                    throw new NotImplementedException(nameof(TextureUnion.TexOneofCase.VideoTexture));
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.TryGetTextureUrl(contentProvider, out url);
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

        public static bool TryGetTextureUrl(this Texture self, ISceneContentProvider contentProvider, out string url) =>
            contentProvider.TryGetMediaUrl(self.Src, out url);

        public static bool TryGetTextureUrl(this AvatarTexture self, out string url) =>
            throw new NotImplementedException(nameof(AvatarTexture));

        public static TextureWrapMode GetWrapMode(this Texture self) =>
            self.HasWrapMode ? (TextureWrapMode)self.WrapMode : TextureWrapMode.Clamp;

        public static TextureWrapMode GetWrapMode(this AvatarTexture self) =>
            self.HasWrapMode ? (TextureWrapMode)self.WrapMode : TextureWrapMode.Clamp;

        public static TextureWrapMode GetWrapMode(this VideoTexture self) =>
            self.HasWrapMode ? (TextureWrapMode)self.WrapMode : TextureWrapMode.Clamp;

        public static FilterMode GetFilterMode(this Texture self) =>
            (FilterMode)(self.HasFilterMode ? self.FilterMode : TextureFilterMode.TfmBilinear);

        public static FilterMode GetFilterMode(this AvatarTexture self) =>
            (FilterMode)(self.HasFilterMode ? self.FilterMode : TextureFilterMode.TfmBilinear);

        public static FilterMode GetFilterMode(this VideoTexture self) =>
            (FilterMode)(self.HasFilterMode ? self.FilterMode : TextureFilterMode.TfmBilinear);
    }
}
