using System;

namespace SceneRuntime.Factory.WebSceneSource.Cache
{
    public interface IJsSourcesCache
    {
        void Cache(string path, string sourceCode);

        bool TryGet(string path, out string? sourceCode);

        class Fake : IJsSourcesCache
        {
            public void Cache(string path, string sourceCode)
            {
                //ignore
            }

            public bool TryGet(string path, out string? sourceCode)
            {
                sourceCode = null;
                return false;
            }
        }
    }
}
