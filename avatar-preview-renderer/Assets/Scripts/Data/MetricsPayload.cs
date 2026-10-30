using System;

// ReSharper disable InconsistentNaming
namespace Data
{
    /// <summary>
    /// The metrics reply, posted to the embedding page as JSON. Field names are the wire format the
    /// page expects.
    /// </summary>
    // Wire schema: https://github.com/decentraland/common-schemas/blob/main/src/dapps/preview/metrics.ts#/Metrics
    [Serializable]
    public class MetricsPayload
    {
        public int triangles;
        public int materials;
        public int textures;
        public int meshes;
        public int bodies;
        public int entities;
    }
}
