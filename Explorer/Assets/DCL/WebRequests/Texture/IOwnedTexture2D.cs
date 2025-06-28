using System;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.WebRequests
{
    public interface IOwnedTexture2D : IDisposable
    {
        public Texture2D Texture { get; }

        class Const : IOwnedTexture2D
        {
            public Texture2D Texture { get; }

            public Const(Texture2D texture)
            {
                Texture = texture;
            }

            public void Dispose()
            {
                UnityObjectUtils.SafeDestroy(Texture);
            }
        }
    }
}
