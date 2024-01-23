using System;
using UnityEditor;
using UnityEngine;

namespace StylizedGrass
{
    [CustomEditor(typeof(StylizedGrassRenderer))]
    public class StylizedGrassRendererInspector : Editor
    {
        StylizedGrassRenderer script;
        SerializedProperty colorMap;
        SerializedProperty listenToWindZone;
        SerializedProperty windZone;

        private SerializedProperty windAmbientMultiplier;
        private SerializedProperty windGustMultiplier;
        
        private bool renderFeaturePresent;
        private void OnEnable()
        {
            script = (StylizedGrassRenderer)target;
            
            #if URP
            renderFeaturePresent = PipelineUtilities.RenderFeatureAdded<GrassRenderFeature>();
            #endif
            
            colorMap = serializedObject.FindProperty("colorMap");
            listenToWindZone = serializedObject.FindProperty("listenToWindZone");
            windZone = serializedObject.FindProperty("windZone");
            windAmbientMultiplier = serializedObject.FindProperty("windAmbientMultiplier");
            windGustMultiplier = serializedObject.FindProperty("windGustMultiplier");
        }

        public override void OnInspectorGUI()
        {
            StylizedGrassGUI.DrawHeader();

#if !URP
            EditorGUILayout.HelpBox("The Universal Render Pipeline v" + AssetInfo.MIN_URP_VERSION + " is not installed", MessageType.Error);
#else

            if (!renderFeaturePresent)
            {
                EditorGUILayout.HelpBox("The Grass Render Feature hasn't been added\nto the current renderer", MessageType.Error);

                GUILayout.Space(-32);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        AddRenderFeature();
                    }
                    GUILayout.Space(8);
                }
                GUILayout.Space(11);
            }


            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(colorMap, new GUIContent("Active color map"));

            if (colorMap.objectReferenceValue == null && GrassColorMapRenderer.Instance)
            {
                EditorGUILayout.HelpBox("A Colormap Renderer component is present, you don't have to assign a colormap in this case", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PropertyField(listenToWindZone);
            EditorGUI.indentLevel++;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (listenToWindZone.boolValue)
                {
                    EditorGUILayout.PropertyField(windZone);

                    if (!windZone.objectReferenceValue)
                    {
                        if (GUILayout.Button("Create", GUILayout.MaxWidth(75f)))
                        {
                            GameObject windZoneGameObject = new GameObject("Wind Zone", new []{ typeof(WindZone) });

                            windZone.objectReferenceValue = windZoneGameObject.GetComponent<WindZone>();

                            EditorGUIUtility.PingObject(windZoneGameObject);
                            Selection.activeGameObject = windZoneGameObject;
                        }
                    }
                    else
                    {
                        if(GUILayout.Button("Edit", GUILayout.MaxWidth(65f)))
                        {
                            EditorGUIUtility.PingObject(windZone.objectReferenceValue);
                            Selection.activeObject = windZone.objectReferenceValue;
                        }
                    }

                }
            }
            
            if (listenToWindZone.boolValue && windZone.objectReferenceValue)
            {
                EditorGUILayout.PropertyField(windAmbientMultiplier);
                EditorGUILayout.PropertyField(windGustMultiplier);
            }
            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (colorMap.objectReferenceValue) script.colorMap.SetActive();
            }

            StylizedGrassGUI.DrawFooter();
#endif
        }

        #if URP
        private void AddRenderFeature()
        {
            PipelineUtilities.AddRenderFeature<GrassRenderFeature>();
            renderFeaturePresent = true;
        }
        #endif

        public override bool HasPreviewGUI()
        {
            return script.vectorRT;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Grass bending vectors");
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            #if URP
            if (!script.vectorRT) return;

            GUI.DrawTexture(r, script.vectorRT, ScaleMode.ScaleToFit);

            Rect btnRect = r;
            btnRect.x += 5f;
            btnRect.y += 5f;
            btnRect.width = 150f;
            btnRect.height = 20f;
            script.debug = GUI.Toggle(btnRect, script.debug, new GUIContent(" Pin to viewport"));

            GUI.Label(new Rect(r.width * 0.5f - (175 * 0.5f), r.height - 5, 175, 25), string.Format("{0} texel(s) per meter", ColorMapRendering.GetTexelSize(script.vectorRT.height, GrassRenderFeature.RenderBendVectors.CurrentResolution)), EditorStyles.toolbarButton);
            #endif
        }
    }
}
