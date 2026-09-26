using DCL.Emoji;
using UnityEditor;
using UnityEngine;

namespace DCL.EmojiPanel.Editor
{
    [CustomEditor(typeof(EmojiPanelConfigurationSO))]
    public class EmojiPanelConfigurationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EmojiPanelConfigurationSO myData = (EmojiPanelConfigurationSO)target;

            myData.emojiSpriteAsset = (ScriptableObject)EditorGUILayout.ObjectField(
                "Sprite Asset",
                myData.emojiSpriteAsset,
                typeof(ScriptableObject),
                false
            );

            // JSON file picker (TextAsset)
            myData.emojiJsonMetadata = (TextAsset)EditorGUILayout.ObjectField(
                "JSON File",
                myData.emojiJsonMetadata,
                typeof(TextAsset),
                false
            );

            if (GUILayout.Button("Load Definitions"))
            {
                myData.LoadFromJson();
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(myData);
            }
        }
    }
}
