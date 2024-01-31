using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(TerrainGeneratorTest))]
    public class TerrainGeneratorTestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Generate"))
            {
                TerrainGeneratorTest generator = (TerrainGeneratorTest)target;
                generator.Generate().Forget();
            }

            base.OnInspectorGUI();
        }
    }
}
