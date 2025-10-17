using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    public class Texture2DData
    {
        public readonly Texture2D Texture;

        public Texture2DData(Texture2D texture2D)
        {
            Texture = texture2D;
        }

        internal void DestroyObject() =>
            UnityObjectUtils.SafeDestroy(Texture);

        public static implicit operator Texture2DData(Texture2D texture2D) =>
            new (texture2D);
    }
}
