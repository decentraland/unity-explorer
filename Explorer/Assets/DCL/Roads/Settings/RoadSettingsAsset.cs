using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Roads.Settings
{
    [Serializable]
    [CreateAssetMenu(menuName = "Create Road Settings", fileName = "RoadSettings", order = 0)]
    public class RoadSettingsAsset : ScriptableObject, IRoadSettingsAsset
    {
        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;

#if UNITY_EDITOR

        /// <summary>
        /// A custom inspector that just shows a search box to make finding road descriptions easier.
        /// </summary>
        [CustomEditor(typeof(RoadSettingsAsset))]
        private class RoadSettingsAssetEditor : Editor
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
                Color originalColor = GUI.contentColor;
                GUI.contentColor = Color.cyan;

                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.LabelField("Search for a Road description by its coordinates:");

                    EditorGUI.BeginChangeCheck();
                    {
                        coordinatesToSearch = EditorGUILayout.Vector2IntField(GUIContent.none, coordinatesToSearch);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        foundElementIndex = -1;
                        showUpdatedMessage = false;
                        showNotFoundMessage = false;
                    }

                    if (GUILayout.Button("Search"))
                    {
                        List<RoadDescription> descriptions = (target as RoadSettingsAsset).RoadDescriptions;

                        for (int i = 0; i < descriptions.Count; ++i)
                        {
                            if (descriptions[i].RoadCoordinate == coordinatesToSearch)
                            {
                                foundElementIndex = i;
                                foundElementCoordinates = descriptions[i].RoadCoordinate;
                                foundElementRotation = descriptions[i].Rotation.eulerAngles;
                                foundElementModel = descriptions[i].RoadModel;
                                break;
                            }
                        }

                        if(foundElementIndex == -1)
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
                            List<RoadDescription> descriptions = (target as RoadSettingsAsset).RoadDescriptions;
                            descriptions[foundElementIndex] = new RoadDescription()
                                                                    {
                                                                        RoadModel = foundElementModel,
                                                                        RoadCoordinate = foundElementCoordinates,
                                                                        Rotation = Quaternion.Euler(foundElementRotation.x, foundElementRotation.y, foundElementRotation.z)
                                                                    };
                            showUpdatedMessage = true;
                        }
                    }

                    GUI.contentColor = originalColor;

                    if(showNotFoundMessage)
                        EditorGUILayout.HelpBox("There is no element with coordinates " + coordinatesToSearch, MessageType.Error);

                    if (showUpdatedMessage)
                        EditorGUILayout.HelpBox("Element updated successfully.", MessageType.Info);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Separator();

                base.OnInspectorGUI();
            }
        }

#endif
    }
}
