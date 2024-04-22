using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(TerrainGeneratorTest))]
    public class TerrainGeneratorTestEditor : UnityEditor.Editor
    {
        private TerrainGeneratorTest generatorTest;

        public override void OnInspectorGUI()
        {
            var shouldDisable = false;
            if (generatorTest != null)
            {
                var generator = generatorTest.GetGenerator();
                shouldDisable = generator is { IsTerrainGenerated: false };
            }

            GUI.enabled = !shouldDisable;
            if (GUILayout.Button("Generate"))
            {
                this.generatorTest = (TerrainGeneratorTest)target;
                this.generatorTest.GenerateAsync().Forget();
            }
            GUI.enabled = true;

            base.OnInspectorGUI();
        }
    }
}
