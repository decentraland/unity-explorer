using UnityEngine;

namespace Utility.Primitives
{
    public static class DefaultMaterial
    {
        private static Material cached;

        private static readonly Shader CACHED_SHADER = Shader.Find("Universal Render Pipeline/Lit");

        public static Material Shared => cached == null ? cached = New() : cached;

        public static Material New() =>
            new (CACHED_SHADER);
    }
}
