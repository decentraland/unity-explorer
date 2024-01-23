using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer
{
    public struct VideoTextureComponent
    {
        public readonly Texture2D texture;

        public VideoTextureComponent(Texture2D texture)
        {
            this.texture = texture;
        }
    }
}
