using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace StylizedGrass
{
    [CustomEditor(typeof(GrassColorMapRenderer))]
    public class GrassColorMapRendererInspector : Editor
    {
        GrassColorMapRenderer script;
        SerializedProperty colorMap;
        SerializedProperty resIdx;
        SerializedProperty resolution;
        SerializedProperty cullingMask;
        SerializedProperty textureDetail;
        SerializedProperty useOriginalMaterials;
        SerializedProperty terrainObjects;

        private TerrainLayer[] terrainLayers;
        private GUIContent[] layerNames;

        private static string iconPrefix => EditorGUIUtility.isProSkin ? "d_" : "";
        private static GUIContent RenderButtonContent;

        private bool expandRenderArea
        {
            get => EditorPrefs.GetBool(PlayerSettings.productName + "_COLORMAP_UI_expandRenderArea", false);
            set => EditorPrefs.SetBool(PlayerSettings.productName + "_COLORMAP_UI_expandRenderArea", value);
        }
        private bool expandLayerScales        
        {
            get => EditorPrefs.GetBool(PlayerSettings.productName + "_COLORMAP_UI_expandLayerScales", false);
            set => EditorPrefs.SetBool(PlayerSettings.productName + "_COLORMAP_UI_expandLayerScales", value);
        }
        private bool expandRendering        
        {
            get => EditorPrefs.GetBool(PlayerSettings.productName + "_COLORMAP_UI_expandRendering", false);
            set => EditorPrefs.SetBool(PlayerSettings.productName + "_COLORMAP_UI_expandRendering", value);
        }

        private bool hasMeshRenderers;

        private void OnEnable()
        {
            script = (GrassColorMapRenderer)target;

            colorMap = serializedObject.FindProperty("colorMap");
            resIdx = serializedObject.FindProperty("resIdx");
            resolution = serializedObject.FindProperty("resolution");
            cullingMask = serializedObject.FindProperty("cullingMask");
            textureDetail = serializedObject.FindProperty("textureDetail");
            useOriginalMaterials = serializedObject.FindProperty("useOriginalMaterials");
            terrainObjects = serializedObject.FindProperty("terrainObjects");

            RefreshTerrainLayers();
            Validate();

            //Update to 1.4.0
            if (!script.terrainAlbedoShader || !script.terrainAlbedoAddPassShader || !script.terrainSplatMaskShader)
            {
                script.terrainAlbedoShader = Shader.Find(ColorMapRendering.TERRAIN_ALBEDO_SHADER_NAME);
                script.terrainAlbedoAddPassShader = Shader.Find(ColorMapRendering.TERRAIN_ALBEDO_ADDPASS_SHADER_NAME);
                script.terrainSplatMaskShader = Shader.Find(ColorMapRendering.TERRAIN_SPLAT_MASK_NAME);
                
                Debug.Log("[Update to v1.4.0+] Grass Color Map Renderer: Serialized a reference to 3 shaders that are required for rendering to the component. Be sure to save the scene");
                EditorUtility.SetDirty(script);
            }
            
            RenderButtonContent  = new GUIContent("  Render", EditorGUIUtility.IconContent(iconPrefix + "Animation.Record").image);
        }

        bool editingCollider
        {
            get { return EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(this); }
        }

        static Color s_HandleColor = new Color(127f, 214f, 244f, 100f) / 255;
        static Color s_HandleColorSelected = new Color(127f, 214f, 244f, 210f) / 255;
        static Color s_HandleColorDisabled = new Color(127f * 0.75f, 214f * 0.75f, 244f * 0.75f, 100f) / 255;
        BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();

        Bounds GetBounds()
        {
            return script.colorMap.bounds;
        }

        private void Validate()
        {
            hasMeshRenderers = false;
            for (int i = 0; i < script.terrainObjects.Count; i++)
            {
                if (script.terrainObjects[i] && script.terrainObjects[i].GetComponent<MeshRenderer>() || script.terrainObjects[i].GetComponent<LODGroup>())
                {
                    hasMeshRenderers = true;
                    return;
                }
            }
        }
        public override void OnInspectorGUI()
        {
            StylizedGrassGUI.DrawHeader();

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(colorMap);
                
                if (GUILayout.Button(new GUIContent(" New", EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_editicon.sml" : "editicon.sml").image), GUILayout.MaxWidth(70f)))
                {
                    colorMap.objectReferenceValue = GrassColorMap.CreateNew();
                    
                    serializedObject.ApplyModifiedProperties();
                }
            }

            if (!colorMap.objectReferenceValue)
            {
                EditorGUILayout.HelpBox("No color map assigned", MessageType.Error);
                return;
            }

            if (colorMap.objectReferenceValue)
            {
                //EditorGUILayout.LabelField(string.Format("Area size: {0}x{1}", script.colorMap.bounds.size.x, script.colorMap.bounds.size.z));

                if (EditorUtility.IsPersistent(script.colorMap) == false && terrainObjects.arraySize > 0)
                {
                    Action saveColorMap = new Action(SaveColorMap);
                    StylizedGrassGUI.DrawActionBox("\n" +
                                                   "  The color map asset has not been saved to a file" +
                                                   "\n     - It can only be used in this scene (not a prefab)." +
                                                   "\n     - No texture compression can be applied." +
                                                   "\n", 
                        new GUIContent("  Save as asset file", EditorGUIUtility.IconContent("SaveActive").image), MessageType.Info, saveColorMap);
                }

                if (script.colorMap.overrideTexture)
                {
                    EditorGUILayout.HelpBox("The assigned color map uses a texture override. Rendering a new/updated color map will revert this.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            
            if (terrainObjects.arraySize == 0) terrainObjects.isExpanded = true;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(terrainObjects, new GUIContent("Terrain(s)"));

            if (EditorGUI.EndChangeCheck())
            {
                Validate();
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent(" Quick actions", EditorGUIUtility.IconContent("Settings").image)))
                {
                    GenericMenu menu = new GenericMenu();
                    
                    menu.AddItem(new GUIContent("Add active terrains (" + Terrain.activeTerrains.Length + ")"), false, () =>
                    {
                        AssignActiveTerrains();
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    menu.AddItem(new GUIContent("Add child meshes"), false, () =>
                    {
                        script.AssignChildMeshes();
                        Validate();
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });

                    #if VEGETATION_STUDIO_PRO
                    menu.AddItem(new GUIContent("Add VSP mesh terrains"), false, () =>
                    {
                        script.AssignVegetationStudioMeshTerrains();
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    #endif
                    
                    if (terrainObjects.arraySize != 0)
                    {
                        menu.AddSeparator(string.Empty);
                        
                        menu.AddItem(new GUIContent("Clear list"), false, () =>
                        {
                            terrainObjects.ClearArray();
                            hasMeshRenderers = false;
                            serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }
            }
            
            EditorGUILayout.Space();
            
            if (terrainObjects.arraySize == 0)
            {
                EditorGUILayout.HelpBox("\n" +
                                        "Assign the target terrain objects to the list above to render." +
                                        "\n\nThese can be Unity terrain objects, or regular Mesh Renderers." +
                                        "\n\nUse the \"Quick actions\" dropdown menu to automatically search for and assign suitable objects in the scene." +
                                        "\n", MessageType.Warning);
            }
            else
            {
                expandRenderArea = EditorGUILayout.BeginFoldoutHeaderGroup(expandRenderArea, "Render area");
                if(expandRenderArea)
                {
                    EditorGUILayout.Space();

                    EditMode.DoEditModeInspectorModeButton(EditMode.SceneViewEditMode.Collider, "Edit Volume", EditorGUIUtility.IconContent("EditCollider"), GetBounds, this);
                    script.colorMap.bounds.size = EditorGUILayout.Vector3Field("Size", script.colorMap.bounds.size);
                    script.colorMap.bounds.center = EditorGUILayout.Vector3Field("Center", script.colorMap.bounds.center);

                    if (script.colorMap.bounds.size == Vector3.zero && terrainObjects.arraySize == 0) EditorGUILayout.HelpBox("The render area cannot be zero", MessageType.Error);

                    using (new EditorGUI.DisabledGroupScope(script.terrainObjects.Count == 0))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Calculate from terrain(s)"))
                            {
                                ColorMapRendering.ApplyUVFromTerrainBounds(colorMap.objectReferenceValue as GrassColorMap, script);
                                EditorUtility.SetDirty(target);

                                SceneView.RepaintAll();
                            }
                        }
                    }
                    
                    EditorGUILayout.Space();

                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                
                EditorGUILayout.Space();

                expandLayerScales = EditorGUILayout.BeginFoldoutHeaderGroup(expandLayerScales, new GUIContent("Layer-based grass scale", 
                    "Renders a scale value, based on the terrain layer's painted strength. Stored in the alpha channel of the rendered color map." +
                    "\n\nThis data can be used in a grass material by enabling the \"Apply scale map\" feature under the \"Vertices\" section."));
                if (expandLayerScales)
                {
                    EditorGUILayout.Space();
                    
                    DrawLayerHeightSettings();
                    
                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.Space();

                expandRendering = EditorGUILayout.BeginFoldoutHeaderGroup(expandRendering, "Rendering");
                if (expandRendering)
                {
                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(cullingMask);

                    if (cullingMask.intValue == 0) EditorGUILayout.HelpBox("The render layer is set to \"Nothing\", no objects will be rendered into the color map", MessageType.Error);

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(useOriginalMaterials, new GUIContent("Use original materials", useOriginalMaterials.tooltip));

                    if (useOriginalMaterials.boolValue == false)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(EditorGUIUtility.labelWidth);
                            EditorGUILayout.HelpBox("The terrains are temporarily rendered using an Unlit terrain shader.", MessageType.None);
                        }
                        
                        if (hasMeshRenderers)
                        {
                            EditorGUILayout.HelpBox("One or more terrain objects is a Mesh Renderer, this option must be enabled to correctly capture their albedo color.", MessageType.Warning);
                        }
                        
                        EditorGUILayout.PropertyField(textureDetail);
                        //EditorGUILayout.LabelField("Mip map level: " + ColorMapEditor.DetailPercentageToMipLevel(textureDetail.floatValue), EditorStyles.miniLabel);
                    }
                    else
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(EditorGUIUtility.labelWidth);
                            EditorGUILayout.HelpBox("The scene lighting is temporarily altered to represent the best possible flat lighting conditions.", MessageType.None);
                        }

                    }
                    
                    EditorGUILayout.Space();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        resIdx.intValue = EditorGUILayout.Popup("Resolution", resIdx.intValue, 
                            new string[] { "64x64", "128x128", "256x256", "512x512", "1024x1024", "2048x2048", "4096x4096" }, 
                            GUILayout.MaxWidth(EditorGUIUtility.labelWidth + 100f));
                    }
                    
                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            
                EditorGUILayout.Space();

                if (script.colorMap.bounds.size == Vector3.zero && terrainObjects.arraySize > 0)
                {
                    EditorGUILayout.HelpBox("The render area is 0. It will be automatically calculated based on the total terrain size", MessageType.Info);
                    EditorGUILayout.Space();
                }

                if (colorMap.objectReferenceValue && Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("[Play mode] To improve runtime baking performance, no rendering data will be saved to disk. Instead the render result will be kept in memory and sent directly to the shader.", MessageType.Info);
                }
                
                if (GUILayout.Button(RenderButtonContent, GUILayout.Height(30f)))
                {
                    script.Render();
                }
                
            }//If terrains assigned

            if (EditorGUI.EndChangeCheck())
            {
                resolution.intValue = ColorMapRendering.IndexToResolution(resIdx.intValue);
                serializedObject.ApplyModifiedProperties();
            }
            
            StylizedGrassGUI.DrawFooter();
        }
        
        private void AssignActiveTerrains()
        {
            script.AssignActiveTerrains();
            
            RefreshTerrainLayers();
        }

        private void DrawLayerHeightSettings()
        {
            if (layerNames == null)
            {
                EditorGUILayout.HelpBox("This feature only works with Unity terrains (The first item in the Terrain Objects list isn't a terrain)", MessageType.Info);
                return;
            }

            if (script.layerScaleSettings != null && script.layerScaleSettings.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUI.BeginChangeCheck();

                    for (int i = 0; i < script.layerScaleSettings.Count; i++)
                    {
                        GrassColorMapRenderer.LayerScaleSettings s = script.layerScaleSettings[i];

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            s.layerIndex = EditorGUILayout.Popup(s.layerIndex, layerNames, GUILayout.MaxWidth(150f));
                            float strength = s.strength * 100f;
                            strength = EditorGUILayout.Slider(strength, 1f, 100f);
                            s.strength = strength * 0.01f;
                            EditorGUILayout.LabelField("%", GUILayout.MaxWidth(17f));

                            if (GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent(iconPrefix + "TreeEditor.Trash").image, "Delete item")))
                            {
                                script.layerScaleSettings.RemoveAt(i);
                            }

                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (colorMap.objectReferenceValue)
                        {
                            //Too slow to do the rendering and writing to disk so frequently
                            if (EditorUtility.IsPersistent(script.colorMap) == false)
                            {
                                script.Render();
                            }
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No scale settings for terrain layers, grass will stay a uniform scale", MessageType.None);
            }


            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Add layer setting", EditorGUIUtility.IconContent(iconPrefix + "Toolbar Plus").image)))
                {
                    if (script.terrainObjects.Count == 0)
                    {
                        Debug.LogError("No terrains assigned");
                        return;
                    }
                    
                    GenericMenu menu = new GenericMenu();
                    
                    for (int i = 0; i < terrainLayers.Length; i++)
                    {
                        if(terrainLayers[i] == null) continue;
            
                        //Check if layer already added
                        if (script.layerScaleSettings.Find(x => x.layerIndex == i) == null)
                        {
                            menu.AddItem(new GUIContent(terrainLayers[i].name), false, AddTerrainLayerMask, i);
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent(terrainLayers[i].name), true);
                        }
                    }
                    
                    menu.ShowAsContext();
                }
            }
        }
        
        private void AddTerrainLayerMask(object id)
        {
            GrassColorMapRenderer.LayerScaleSettings s = new GrassColorMapRenderer.LayerScaleSettings();
            s.layerIndex = (int)id;
            
            script.layerScaleSettings.Add(s);

            //Too slow to do the rendering and writing to disk so frequently
            if (EditorUtility.IsPersistent(script.colorMap) == false)
            {
                script.Render();
            }
        }

        private void RefreshTerrainLayers()
        {
            if (script.terrainObjects.Count == 0)
            {
                layerNames = null;
                return;
            }

            Terrain t = script.terrainObjects[0].GetComponent<Terrain>();

            if (t == null)
            {
                layerNames = null;
                return;
            }


            terrainLayers = t.terrainData.terrainLayers;
            
            layerNames = new GUIContent[terrainLayers.Length];
            for (int i = 0; i < layerNames.Length; i++)
            {
                layerNames[i] = new GUIContent(t.terrainData.terrainLayers[i] ? t.terrainData.terrainLayers[i].name : "(Missing)");
            }

        }

        public override bool HasPreviewGUI()
        {
            return script.colorMap && script.colorMap.texture;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Output");
        }

        public override void OnPreviewSettings()
        {
            if (script.colorMap.texture == false) return;

            GUILayout.Label($"Resolution ({script.colorMap.texture.width}x{script.colorMap.texture.height})");
        }

        private bool previewColor = true;
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (script.colorMap.texture == null) return;

            if (previewColor)
            {
                GUI.DrawTexture(r, script.colorMap.texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                EditorGUI.DrawTextureAlpha(r, script.colorMap.texture, ScaleMode.ScaleToFit);
            }

            Rect btnRect = r;
            btnRect.x += 10f;
            btnRect.y += 10f;
            btnRect.width = 50f;
            btnRect.height = 20f;

            previewColor = GUI.Toggle(btnRect, previewColor, new GUIContent("Color"), "Button");
            btnRect.x += 49f;
            previewColor = !GUI.Toggle(btnRect, !previewColor, new GUIContent("Scale"), "Button");
            
            GUI.Label(new Rect(r.width * 0.5f - (175 * 0.5f), r.height - 5, 175, 25), $"{ColorMapRendering.GetTexelSize(script.colorMap.texture.width, script.colorMap.bounds.size.x)} texel(s) per unit", EditorStyles.toolbarButton);
        }

        private void SaveColorMap()
        {
            ColorMapRendering.SaveColorMapToAsset(script, colorMap.objectReferenceValue as GrassColorMap);
        }

        void OnSceneGUI()
        {

            if (!editingCollider || script.colorMap == null)
                return;

            Bounds bounds = script.colorMap.bounds;
            Color color = script.enabled ? s_HandleColor : s_HandleColorDisabled;
            using (new Handles.DrawingScope(color, Matrix4x4.identity))
            {
                m_BoundsHandle.center = bounds.center;
                m_BoundsHandle.size = bounds.size;

                EditorGUI.BeginChangeCheck();
                m_BoundsHandle.DrawHandle();
                //m_BoundsHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z;

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(script.colorMap, "Modified Grass color map bounds");
                    Vector3 center = m_BoundsHandle.center;
                    Vector3 size = m_BoundsHandle.size;

                    script.colorMap.bounds.center = center;
                    script.colorMap.bounds.size = size;
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
