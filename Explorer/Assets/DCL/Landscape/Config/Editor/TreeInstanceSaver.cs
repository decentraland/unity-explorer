using DCL.Landscape.Settings;
using Decentraland.Terrain;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TreePrototype = UnityEngine.TreePrototype;

namespace DCL.Landscape.Config.Editor
{
    public sealed class TreeInstanceSaver : ScriptableWizard
    {
        [field: SerializeField] private TerrainGenerationData TerrainData { get; set; }

        [MenuItem("Decentraland/Landscape/Save Tree Instances")]
        private static void OnMenuItem() =>
            DisplayWizard<TreeInstanceSaver>("Save Tree Instances", "Save");

        private void OnWizardCreate()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            if (terrains.Length == 0)
                return;

            TreePrototype[] unityTreePrototypes = terrains[0].terrainData.treePrototypes;
            var treePrototypes = new Decentraland.Terrain.TreePrototype[unityTreePrototypes.Length];

            for (int i = 0; i < treePrototypes.Length; i++)
            {
                TreePrototype unityPrototype = unityTreePrototypes[i];

                LandscapeAsset landscapeAsset = TerrainData.treeAssets
                                                           .Single(i =>
                                                                i.asset == unityPrototype.prefab);

                ObjectRandomization rand = landscapeAsset.randomization;

                if (rand.proportionalScale)
                {
                    treePrototypes[i] = new Decentraland.Terrain.TreePrototype()
                    {
                        MinScaleXZ = rand.randomScale.x,
                        MaxScaleXZ = rand.randomScale.y,
                        MinScaleY = rand.randomScale.x,
                        MaxScaleY = rand.randomScale.y
                    };
                }
                else
                {
                    treePrototypes[i] = new Decentraland.Terrain.TreePrototype()
                    {
                        MinScaleXZ = (rand.randomScaleX.x + rand.randomScaleZ.x) * 0.5f,
                        MaxScaleXZ = (rand.randomScaleX.y + rand.randomScaleZ.y) * 0.5f,
                        MinScaleY = rand.randomScaleY.x,
                        MaxScaleY = rand.randomScaleY.y
                    };
                }
            }

            var writer = new TreeInstanceWriter(TerrainData.parcelSize, treePrototypes);

            foreach (Terrain terrain in terrains)
                writer.AddTerrain(terrain);

            string path = $"{Application.streamingAssetsPath}/TreeInstances.bin";

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                writer.Write(stream);
        }
    }
}
