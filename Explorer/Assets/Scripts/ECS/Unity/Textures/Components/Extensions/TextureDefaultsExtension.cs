using CommunicationData.URLHelpers;
using Decentraland.Common;
using SceneRunner.Scene;
using UnityEngine;
using Texture = Decentraland.Common.Texture;
using TextureWrapMode = UnityEngine.TextureWrapMode;
using Vector2 = UnityEngine.Vector2;

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

            bool success = self.TryGetTextureUrl(data, out URLAddress url);
            self.TryGetTextureFileHash(data, out string fileHash);

            return success
                ? new TextureComponent(url, fileHash, self.GetWrapMode(), self.GetFilterMode(), textureOffset: self.GetOffset(), textureTiling: self.GetTiling())
                : null;
        }

        public static bool TryGetTextureUrl(this TextureUnion self, ISceneData data, out URLAddress url)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    return self.AvatarTexture.TryGetTextureUrl(out url);
                case TextureUnion.TexOneofCase.VideoTexture:
                    url = URLAddress.EMPTY; // just ignore to not break the loop
                    return false;
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.TryGetTextureUrl(data, out url);
            }
        }

        public static bool TryGetTextureFileHash(this TextureUnion self, ISceneData data, out string fileHash)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                    return self.AvatarTexture.TryGetTextureFileHash(out fileHash);
                case TextureUnion.TexOneofCase.VideoTexture:
                    fileHash = string.Empty;
                    return false;
                case TextureUnion.TexOneofCase.Texture:
                default:
                    return self.Texture.TryGetTextureFileHash(data, out fileHash);
            }
        }

        public static int GetVideoTextureId(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.VideoTexture:
                    return (int)self.VideoTexture.VideoPlayerEntity;
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

        public static Vector2 GetOffset(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                case TextureUnion.TexOneofCase.VideoTexture:
                default:
                    return Vector2.zero;
                case TextureUnion.TexOneofCase.Texture:
                    return self.Texture.Offset ?? Vector2.zero;
            }
        }

        public static Vector2 GetTiling(this TextureUnion self)
        {
            switch (self.TexCase)
            {
                case TextureUnion.TexOneofCase.AvatarTexture:
                case TextureUnion.TexOneofCase.VideoTexture:
                default:
                    return Vector2.one;
                case TextureUnion.TexOneofCase.Texture:
                    return self.Texture.Tiling ?? Vector2.one;
            }
        }

        public static bool TryGetTextureUrl(this Texture self, ISceneData data, out URLAddress url) =>
            data.TryGetMediaUrl(self.Src, out url);

        public static bool TryGetTextureFileHash(this Texture self, ISceneData data, out string fileHash) =>
            data.TryGetMediaFileHash(self.Src, out fileHash);

        public static bool TryGetTextureUrl(this AvatarTexture self, out URLAddress url)
        {
            // Not implemented
            url = URLAddress.EMPTY;
            return false;
        }

        public static bool TryGetTextureFileHash(this AvatarTexture self, out string fileHash)
        {
            // Not implemented
            fileHash = string.Empty;
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
