using UnityEngine;

namespace Utility.Primitives
{
    public static class DefaultMaterial
    {
        private static readonly Shader CACHED_SHADER = Shader.Find("DCL/Universal Render Pipeline/Lit");
        private static Material cached;

        public static Material Shared => cached == null ? cached = New() : cached;

        public static Material New() =>
            new (CACHED_SHADER);
    }
}
