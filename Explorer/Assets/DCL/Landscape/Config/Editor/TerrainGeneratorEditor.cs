using UnityEditor;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(TerrainGenerator))]
    public class TerrainGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            bool newChanges = DrawDefaultInspector();

            if (newChanges)
                if (target is TerrainGenerator terrainGenerator)
                    if (terrainGenerator.liveUpdates)
                        terrainGenerator.GenerateTerrainGrid();
        }
    }
}
