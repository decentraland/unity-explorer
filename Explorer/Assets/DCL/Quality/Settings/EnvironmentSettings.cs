using System;

namespace DCL.Quality
{
    [Serializable]
    public class EnvironmentSettings
    {
        public int sceneLoadRadius;
        public int lod1Threshold;
        public int lod2Threshold;
        public float terrainLODBias;
        public float detailDensity;
        public float grassDistance;
        public float chunkCullDistance;
    }
}
