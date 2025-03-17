using UnityEditor;

namespace DCL.Landscape.Config.Editor
{
    public static class TerrainTools
    {
        [MenuItem("Decentraland/Clear Terrains Cache")]
        public static void CleanTerrainsCache()
        {
            TerrainGeneratorTest.CleanTerrainsCache();
        }
    }
}
