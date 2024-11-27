using UnityEditor;
using UnityEngine;

namespace DCL.Roads.Settings.Editor
{
    /// <summary>
    /// A custom inspector that just shows a search box to make finding road descriptions easier.
    /// </summary>
    [CustomEditor(typeof(RoadSettingsAsset))]
    internal class RoadSettingsAssetEditor : UnityEditor.Editor
    {
        private Vector2Int coordinatesToSearch;
        private int foundElementIndex = -1;
        private Vector2Int foundElementCoordinates;
        private Vector3 foundElementRotation;
        private string foundElementModel;
        private bool showNotFoundMessage;
        private bool showUpdatedMessage;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Search for a Road description by its coordinates:");

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                coordinatesToSearch = EditorGUILayout.Vector2IntField(GUIContent.none, coordinatesToSearch);

                if (scope.changed)
                {
                    foundElementIndex = -1;
                    showUpdatedMessage = false;
                    showNotFoundMessage = false;
                }
            }

            RoadSettingsAsset roadSettingsAsset = target as RoadSettingsAsset;

            if (GUILayout.Button("Search"))
            {
                for (int i = 0; i < roadSettingsAsset.RoadDescriptions.Count; ++i)
                {
                    if (roadSettingsAsset.RoadDescriptions[i].RoadCoordinate == coordinatesToSearch)
                    {
                        foundElementIndex = i;
                        foundElementCoordinates = roadSettingsAsset.RoadDescriptions[i].RoadCoordinate;
                        foundElementRotation = roadSettingsAsset.RoadDescriptions[i].Rotation.eulerAngles;
                        foundElementModel = roadSettingsAsset.RoadDescriptions[i].RoadModel;
                        break;
                    }
                }

                if (foundElementIndex == -1)
                    showNotFoundMessage = true;
            }

            if (foundElementIndex > -1)
            {
                EditorGUILayout.LabelField("Element found at: " + foundElementIndex);

                foundElementCoordinates = EditorGUILayout.Vector2IntField(nameof(RoadDescription.RoadCoordinate), foundElementCoordinates);
                foundElementRotation = EditorGUILayout.Vector3Field(nameof(RoadDescription.Rotation), foundElementRotation);
                foundElementModel = EditorGUILayout.TextField(nameof(RoadDescription.RoadModel), foundElementModel);

                if (GUILayout.Button("Update road"))
                {
                    roadSettingsAsset.RoadDescriptions[foundElementIndex] = new RoadDescription()
                        {
                            RoadModel = foundElementModel,
                            RoadCoordinate = foundElementCoordinates,
                            Rotation = Quaternion.Euler(foundElementRotation.x, foundElementRotation.y, foundElementRotation.z)
                        };

                    showUpdatedMessage = true;
                }
            }

            if (showNotFoundMessage)
                EditorGUILayout.HelpBox("There is no element with coordinates " + coordinatesToSearch, MessageType.Error);

            if (showUpdatedMessage)
                EditorGUILayout.HelpBox("Element updated successfully.", MessageType.Info);

            EditorGUILayout.Separator();

            base.OnInspectorGUI();
        }
    }
}
