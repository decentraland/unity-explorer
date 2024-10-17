using CommunicationData.URLHelpers;
using Decentraland.Common;
using SceneRunner.Scene;
using UnityEngine;
using Texture = Decentraland.Common.Texture;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace ECS.Unity.Textures.Components.Extensions
{
    public static class TextureDefaultsExtensions
    {
        public static TextureComponent? CreateTextureComponent(this TextureUnion? self, ISceneData data)
        {
            if (self == null)
                return null;

            if (self.IsVideoTexture())
            {
                var textureComponent = new TextureComponent(URLAddress.EMPTY, string.Empty, self.GetWrapMode(), self.GetFilterMode(), true, self.GetVideoTextureId());
                return textureComponent;
            }

            return self.TryGetTextureUrl(data, out URLAddress url, out string fileHash)
                ? new TextureComponent(url, fileHash, self.GetWrapMode(), self.GetFilterMode())
                : null;
        }

        public static bool TryGetTextureUrl(this TextureUnion self, ISceneData data, out URLAddress url, out string fileHash)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    fileHash = string.Empty;
                    return self.AvatarTexture.TryGetTextureUrl(out url);
                case TextureUnion.TexOneofCase.VideoTexture:
                    url = URLAddress.EMPTY; // just ignore to not break the loop
                    fileHash = string.Empty;
                    return false;
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.TryGetTextureUrl(data, out url, out fileHash);
            }
        }

        public static int GetVideoTextureId(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.VideoTexture:
                    return (int) self.VideoTexture.VideoPlayerEntity;
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

        public static bool TryGetTextureUrl(this Texture self, ISceneData data, out URLAddress url, out string fileHash) =>
            data.TryGetMediaUrl(self.Src, out url, out fileHash);

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
