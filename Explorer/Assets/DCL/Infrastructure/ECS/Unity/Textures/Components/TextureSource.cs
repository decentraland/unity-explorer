using Decentraland.Common;
using System;

namespace ECS.Unity.Textures.Components
{
    public readonly struct TextureSource : IEquatable<TextureSource>
    {
        public readonly TextureUnion.TexOneofCase TextureType;

        private readonly Uri? Uri;
        private readonly string? UserId;

        public string Id
        {
            get
            {
                return TextureType switch
                       {
                           TextureUnion.TexOneofCase.Texture => Uri!.OriginalString,
                           TextureUnion.TexOneofCase.VideoTexture => string.Empty,
                           TextureUnion.TexOneofCase.AvatarTexture => UserId!,
                           _ => throw new ArgumentOutOfRangeException(nameof(TextureType), TextureType, "Unknown texture type"),
                       };
            }
        }

        public Uri GetUri() =>
            TextureType switch
            {
                TextureUnion.TexOneofCase.Texture => Uri!,
                _ => throw new ArgumentOutOfRangeException(nameof(TextureType), TextureType, "Doesn't have a URI"),
            };

        /// <summary>
        ///     Create a texture from the real URL
        /// </summary>
        /// <param name="uri"></param>
        private TextureSource(Uri uri)
        {
            Uri = uri;
            UserId = null;
            TextureType = TextureUnion.TexOneofCase.Texture;
        }

        /// <summary>
        ///     Create an avatar texture
        /// </summary>
        private TextureSource(string userId)
        {
            UserId = userId;
            Uri = null;
            TextureType = TextureUnion.TexOneofCase.AvatarTexture;
        }

        private TextureSource(TextureUnion.TexOneofCase type)
        {
            TextureType = type;
            Uri = null;
            UserId = null;
        }

        public static TextureSource CreateVideoTexture() =>
            new (TextureUnion.TexOneofCase.VideoTexture);

        public static TextureSource CreateFromUri(Uri uri) =>
            new (uri);

        public static TextureSource CreateFromUserId(string userId) =>
            new (userId);

        public bool Equals(TextureSource other) =>
            TextureType == other.TextureType && Equals(Uri, other.Uri) && UserId == other.UserId;

        public override bool Equals(object? obj) =>
            obj is TextureSource other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)TextureType, Uri, UserId);

        public static bool operator ==(TextureSource left, TextureSource right) =>
            left.Equals(right);

        public static bool operator !=(TextureSource left, TextureSource right) =>
            !left.Equals(right);
    }
}
