using System;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : IDisposable
    {
        public readonly string Source;

        public GLTFData(string source)
        {
            Source = source;
        }

        public void Dispose()
        {

        }
    }
}
