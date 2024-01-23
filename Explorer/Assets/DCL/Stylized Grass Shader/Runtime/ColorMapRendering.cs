//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if URP
using UnityEngine.Rendering.Universal;
#if !UNITY_2021_2_OR_NEWER
//Backwards compatibility
using UniversalRendererData = UnityEngine.Rendering.Universal.ForwardRendererData;
#endif
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StylizedGrass
{
    public static class ColorMapRendering
    {
        public const string TERRAIN_ALBEDO_SHADER_NAME = "Hidden/TerrainAlbedo";
        public const string TERRAIN_ALBEDO_ADDPASS_SHADER_NAME = "Hidden/TerrainAlbedoAdd";
        public const string TERRAIN_SPLAT_MASK_NAME = "Hidden/TerrainSplatmask";
        
        private const float CLIP_PADDING = 5f;
        private const float HEIGHT_OFFSET = 1000f;
        
        //Doesn't really matter what this is, but something that stands out helps to communicate what is out-of-bounds
        private static readonly Color backgroundColor = Color.red;
        
        //Get all terrains
        private static List<Terrain> terrains = new List<Terrain>();
        
        //Scene lighting state
        private static List<Light> dirLights;
        private static Dictionary<Light, bool> originalLightStates = new Dictionary<Light, bool>();
        private static AmbientMode ambientMode;
        private static float reflectionIntensity;
        private static bool fogEnabled;
        private static Color ambientColor;
        
        private static Material splatExtractionMat;
        private static Material unlitTerrainMat;
        
        #if URP
        private static UniversalRendererData rendererData;
        #endif
        private static Camera renderCam;

        private static RenderTexture renderTarget;
        private static Texture2D bakedTexture;

        private struct TerrainState
        {
            public Terrain terrain;
            public bool drawInstanced;
            public float basemapDistance;
            public Material material;
            public bool renderFoliage;
        }
        private static Dictionary<GameObject, TerrainState> originalTerrainStates = new Dictionary<GameObject, TerrainState>();
        
        //Copy the RenderTexture (GPU) to a Texture2D (CPU), with the intent on saving the data to disk.
        private static bool CopyBuffer => Application.isPlaying == false;
        //private static bool CopyBuffer => true;
    
        #region Rendering Setup
        public static void Render(GrassColorMapRenderer renderer)
        {
            if (!renderer.colorMap) renderer.colorMap = ScriptableObject.CreateInstance<GrassColorMap>();
            
            //If no area was defined, automatically calculate it
            if (renderer.colorMap.bounds.size == Vector3.zero)
            {
                renderer.colorMap.bounds = GetTerrainBounds(renderer.terrainObjects);
            }

            //Update UV from current bounds
            renderer.colorMap.uv = BoundsToUV(renderer.colorMap.bounds);

            renderer.colorMap.overrideTexture = false;
            
            terrains.Clear();
            foreach (GameObject item in renderer.terrainObjects)
            {
                if (item == null) continue;
                Terrain t = item.GetComponent<Terrain>();

                if (t) terrains.Add(t);
            }
            
            SetupTerrains(renderer);
            SetupRenderer(renderer);
            SetupLighting(renderer);
            
            RenderToTexture(renderer);

            RestoreLighting(renderer);
            RestoreRenderer();
            RestoreTerrains(renderer);

            Save(renderer, renderer.colorMap);
        }
        
        private static void SetupTerrains(GrassColorMapRenderer renderer)
        {
            originalTerrainStates.Clear();
            
            //Override the material with an Unlit variant
            if(renderer.useOriginalMaterials == false)
            {
                unlitTerrainMat = new Material(renderer.terrainAlbedoShader);
                
                if (!unlitTerrainMat)
                {
                    Debug.LogError($"[ColorMapRendering] Failed to create Unlit terrain material. Renderer likely does not have the \"{TERRAIN_ALBEDO_SHADER_NAME}\" shader assigned");
                    return;
                }
            }
            
            foreach (GameObject item in renderer.terrainObjects)
            {
                if (item == null) continue;
                
                Terrain terrain = item.GetComponent<Terrain>();
                
                //Save original user state
                TerrainState state = new TerrainState
                {
                    terrain = terrain
                };
                
                if (terrain)
                {
                    state.drawInstanced = terrain.drawInstanced;
                    state.basemapDistance = terrain.basemapDistance;
                    state.material = terrain.materialTemplate;
                    state.renderFoliage = terrain.drawTreesAndFoliage;
                }
                originalTerrainStates.Add(item, state);

                //Now safe to modify the terrain for rendering
                
                //Temporarily move everything up and out of the way
                item.transform.position += Vector3.up * HEIGHT_OFFSET;

                if (terrain)
                {
                    //The replacement Unlit shader gets stripped of its instanced variant, because at build-time Unity does not see it being used on any instanced terrains.
                    //To avoid the shader being stripped in a build, restrict terrains to non-instanced rendering.
                    terrain.drawInstanced = false;

                    terrain.basemapDistance = 99999;
                    
                    //Disable, don't want these captured
                    terrain.drawTreesAndFoliage = false;
                    
                    //Override the material with an Unlit variant
                    if(renderer.useOriginalMaterials == false) terrain.materialTemplate = unlitTerrainMat;
                }
            }
        }

        private static void SetupRenderer(GrassColorMapRenderer renderer)
        {
            if(renderCam) Object.DestroyImmediate(renderCam.gameObject);
            
            renderCam = new GameObject().AddComponent<Camera>();
            
            renderCam.name = "Colormap renderCam (temp)";
            renderCam.enabled = false;
            renderCam.hideFlags = HideFlags.HideAndDontSave;

            //Camera set up
            renderCam.orthographic = true;
            renderCam.orthographicSize = Mathf.Max(renderer.colorMap.bounds.extents.x, renderer.colorMap.bounds.extents.z);
            
            renderCam.nearClipPlane = 0.001f;
            renderCam.farClipPlane = renderer.colorMap.bounds.size.y + CLIP_PADDING;
            
            renderCam.cullingMask = renderer.cullingMask;
            
            renderCam.clearFlags = CameraClearFlags.Color;
            renderCam.backgroundColor = backgroundColor;

            //Position cam in given top-center of terrain(s)
            renderCam.transform.position = renderer.colorMap.bounds.center + Vector3.up * (renderer.colorMap.bounds.extents.y + CLIP_PADDING + HEIGHT_OFFSET);
            //Rotate to face down
            renderCam.transform.localEulerAngles = Vector3.right * 90f;

#if URP
            UniversalAdditionalCameraData camData = renderCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = false;
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.requiresDepthOption = CameraOverrideOption.Off;
            camData.requiresColorTexture = false;
            camData.requiresDepthTexture = false;

            if (UniversalRenderPipeline.asset)
            {
                rendererData = PipelineUtilities.CreateEmptyRenderer("Colormap Renderer");
                rendererData.hideFlags = HideFlags.DontSave;

                PipelineUtilities.ValidatePipelineRenderers(rendererData);
                PipelineUtilities.AssignRendererToCamera(camData, rendererData);
            }
            else
            {
                Debug.LogError("[StylizedGrassRenderer] No Universal Render Pipeline is currently active.");
            }
#endif
        }

        private static void SetupLighting(GrassColorMapRenderer renderer)
        {
            //If unable to use an unlit terrain material, set up scene lighting to closely represent plain albedo lighting
            if (renderer.useOriginalMaterials == false) return;
            
            //Setup faux albedo lighting
            #if UNITY_2023_2_OR_NEWER
            var lights = Object.FindObjectsByType(typeof(Light), FindObjectsSortMode.None);
            #else
            var lights = Object.FindObjectsOfType(typeof(Light));
            #endif
            
            dirLights = new List<Light>();
            originalLightStates.Clear();
            
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    //Save the original enabled state
                    originalLightStates.Add(light, light.enabled);
      
                    //Disable it, since it shouldn't influence the scene during rendering
                    light.enabled = false;
                    
                    dirLights.Add(light);
                }
            }

            ambientMode = RenderSettings.ambientMode;
            ambientColor = RenderSettings.ambientLight;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            fogEnabled = RenderSettings.fog;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;
        }
        
        private enum Pass
        {
            IsolateChannel,
            MaxBlend,
            FillWhite,
            AlphaMerge
        }
        
        private static void GenerateScalemap(List<Terrain> terrains, GrassColorMapRenderer renderer, RenderTexture rgb)
        {
            if (terrains.Count == 0) return;

            if (renderer.layerScaleSettings.Count > 0)
            {
                if (!renderer.terrainSplatMaskShader)
                {
                    Debug.LogError($"[ColorMapRendering] Renderer does not have the \"{TERRAIN_SPLAT_MASK_NAME}\" shader assigned");
                    return;
                }
                
                splatExtractionMat = new Material(renderer.terrainSplatMaskShader);
                
                //Temporarily override terrain material
                //Note: This is safe, the actual terrain material will be restored later on
                foreach (Terrain t in terrains) t.materialTemplate = splatExtractionMat;

                RenderTexture alphaBuffer = new RenderTexture(renderer.resolution, renderer.resolution, 8, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
                RenderTexture scalemapBuffer = new RenderTexture(renderer.resolution, renderer.resolution, 8, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
                //Alpha-weights combined makes up the scale map
                RenderTexture scalemap = new RenderTexture(renderer.resolution, renderer.resolution, 8, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

                MaterialPropertyBlock props = new MaterialPropertyBlock();

                //Sort by strength
                //List<GrassColorMapRenderer.LayerScaleSettings> settings = renderer.layerScaleSettings.OrderByDescending(o => o.strength).ToList();

                int currentSplatIndex = 0;
                foreach (GrassColorMapRenderer.LayerScaleSettings layer in renderer.layerScaleSettings)
                {
                    int splatmapID = GetSplatmapID(layer.layerIndex);

                    Shader.SetGlobalVector("_SplatMask", GetVectorMask(layer.layerIndex));
                    Shader.SetGlobalFloat("_SplatChannelStrength", layer.strength);

                    //Terrain render splatmap 0 by default, force to render next splatmap in base pass
                    if (splatmapID != currentSplatIndex)
                    {
                        //Debug.Log("layer.layerID requres splatmap switch to " + splatmapID);

                        foreach (Terrain t in terrains)
                        {
                            props.SetTexture("_Control", t.terrainData.GetAlphamapTexture(splatmapID));
                            t.SetSplatMaterialPropertyBlock(props);
                        }

                        currentSplatIndex = splatmapID;
                    }

                    //Render now visible alpha weight into buffer
                    renderCam.targetTexture = alphaBuffer;
                    renderCam.Render();

                    Shader.SetGlobalTexture("_InputAlphamap", alphaBuffer);
                    Shader.SetGlobalTexture("_InputHeightmap", scalemap);
                    
                    //Max blending copy here!
                    Graphics.Blit(alphaBuffer, scalemapBuffer, splatExtractionMat, (int)Pass.MaxBlend);
                    Graphics.Blit(scalemapBuffer, scalemap);
                }

                //Fill any black pixels with white (taking into account blank splatmap channels)
                Shader.SetGlobalTexture("_InputHeightmap", scalemapBuffer);
                Graphics.Blit(null, scalemap, splatExtractionMat, (int)Pass.FillWhite);
                //Restore
                foreach (Terrain t in terrains)
                {
                    t.SetSplatMaterialPropertyBlock(null);
                }

                //Add heightmap to alpha channel of rgb map
                Shader.SetGlobalTexture("_InputColormap", rgb);
                Shader.SetGlobalTexture("_InputHeightmap", scalemap);

                RenderTexture colorBuffer = new RenderTexture(rgb);
                Graphics.Blit(null, colorBuffer, splatExtractionMat, (int)Pass.AlphaMerge);

                Graphics.Blit(colorBuffer, rgb);

                renderer.colorMap.hasScalemap = true;
                
                //Cleanup
                splatExtractionMat = null;
                colorBuffer.Release();
            }
            else
            {
                renderer.colorMap.hasScalemap = false;
            }
        }
 
        private static void RenderToTexture(GrassColorMapRenderer renderer)
        {
            if (!renderCam)
            {
                Debug.LogError("Renderer does not have a render cam set up");
                return;
            }
            
            if(renderTarget) RenderTexture.ReleaseTemporary(renderTarget);
            //Set up render texture
            renderTarget = RenderTexture.GetTemporary(renderer.resolution, renderer.resolution, 8, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            renderCam.targetTexture = renderTarget;
            RenderTexture.active = renderTarget;
            
            Shader.SetGlobalInt("_ColormapMipLevel", DetailPercentageToMipLevel(renderer.textureDetail));

            //Render camera into a texture
            renderCam.Render();

            //Generate heightmap from terrain layers
            GenerateScalemap(terrains, renderer, renderTarget);

            if (CopyBuffer)
            {
                Graphics.SetRenderTarget(renderTarget);

                bakedTexture = new Texture2D(renderer.resolution, renderer.resolution, TextureFormat.ARGB32, false, true);

                bakedTexture.ReadPixels(new Rect(0, 0, renderer.resolution, renderer.resolution), 0, 0);
                bakedTexture.Apply();

                #if UNITY_EDITOR
                EditorUtility.SetDirty(renderer.colorMap);
                #endif
            }
            
            //Cleanup
            renderCam.targetTexture = null;
            RenderTexture.active = null;

            if (CopyBuffer)
            {
                //Contents are now stored in a Texture2D, so can dispose this
                RenderTexture.ReleaseTemporary(renderTarget);
            }
        }
        #endregion
        
        #region Rendering Post
        private static void RestoreLighting(GrassColorMapRenderer renderer)
        {
            if (renderer.useOriginalMaterials == false) return;
            
            //Restore previously disabled lights
            foreach (Light light in dirLights)
            {
                if (originalLightStates.TryGetValue(light, out var state))
                {
                    light.enabled = state;
                }
            }

            //Restore scene lighting
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = fogEnabled;
        }

        private static void RestoreRenderer()
        {
            Object.DestroyImmediate(renderCam.gameObject);
            renderCam = null;
            
#if URP
            PipelineUtilities.RemoveRendererFromPipeline(rendererData);
#endif
        }
        
        private static void RestoreTerrains(GrassColorMapRenderer renderer)
        {
            foreach (GameObject item in renderer.terrainObjects)
            {
                if (item == null) continue;

                //Get the original user state
                originalTerrainStates.TryGetValue(item, out var state);

                item.transform.position += Vector3.down * HEIGHT_OFFSET;

                if (state.terrain)
                {
                    //Re-enable if originally enabled
                    state.terrain.drawInstanced = state.drawInstanced;
                    state.terrain.drawTreesAndFoliage = state.renderFoliage;

                    state.terrain.basemapDistance = state.basemapDistance;
                    
                    state.terrain.materialTemplate = state.material;
                }
            }
            
            if(renderer.useOriginalMaterials == false) Object.DestroyImmediate(unlitTerrainMat);
        }
        #endregion
        
        #region Saving
        private static void Save(GrassColorMapRenderer renderer, GrassColorMap colorMap)
        {
            if (CopyBuffer)
            {
                #if UNITY_EDITOR
                if (EditorUtility.IsPersistent(colorMap))
                {
                    string colorMapAssetPath = AssetDatabase.GetAssetPath(colorMap);

                    GrassColorMap existingFile = (GrassColorMap)AssetDatabase.LoadAssetAtPath(colorMapAssetPath, typeof(GrassColorMap));
                    EditorUtility.CopySerialized(colorMap, existingFile);

                    {
                        //Update from <= v1.3.4
                        //Also check for sub-assets, are to be removed!
                        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(colorMapAssetPath);
                        if (subAssets.Length > 0)
                        {
                            for (int i = 0; i < subAssets.Length; i++)
                            {
                                if (AssetDatabase.IsMainAsset(subAssets[i])) continue;

                                Object.DestroyImmediate(subAssets[i], true);
                            }
                        }
                    }

                    EditorUtility.SetDirty(existingFile);
                }

                if (colorMap.overrideTexture == false)
                {
                    InitializeTexture(colorMap, renderer.resolution);

                    if (EditorUtility.IsPersistent(colorMap))
                    {
                        SaveTextureData(renderer, colorMap);
                    }
                    else
                    {
                        //Embedded as an instance of the component, simply copy the data
                        Graphics.CopyTexture(bakedTexture, colorMap.texture);
                    }
                }
                #else
                Graphics.CopyTexture(bakedTexture, colorMap.texture);
                #endif
            }
            else
            {
                colorMap.texture = renderTarget;
            }
        }

        private static void InitializeTexture(GrassColorMap colorMap, int resolution)
        {
            if (!colorMap.texture || (colorMap.texture.width != resolution))
            {
                //if(colorMap.texture) Debug.Log($"Recreating texture object, resized. Old:{colorMap.texture.width} New:{resolution}");

                colorMap.texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
                colorMap.texture.name = colorMap.name + " Texture";
            }
        }
        
        public static void SaveColorMapToAsset(GrassColorMapRenderer renderer, GrassColorMap colorMap)
        {
            #if UNITY_EDITOR
            if (CopyBuffer)
            {
                string destFolder = EditorUtility.SaveFolderPanel("Asset destination folder", "Assets/", "");

                //Dialog cancelled
                if (destFolder == string.Empty)
                {
                    return;
                }

                destFolder = destFolder.Replace(Application.dataPath, "Assets");
                string colorMapAssetPath = destFolder + "/" + colorMap.name + ".asset";

                AssetDatabase.CreateAsset(colorMap, colorMapAssetPath);

                Debug.Log("Saved Colormap asset to <i>" + destFolder + "</i> folder");

                //Now load it back
                colorMap = (GrassColorMap)AssetDatabase.LoadAssetAtPath(colorMapAssetPath, typeof(GrassColorMap));

                if (colorMap.overrideTexture == false)
                {
                    InitializeTexture(colorMap, renderer.resolution);

                    SaveTextureData(renderer, colorMap);
                }
            }
            #endif
        }

        private static void SaveTextureData(GrassColorMapRenderer renderer, GrassColorMap colorMap)
        {
            #if UNITY_EDITOR
            string textureAssetPath = AssetDatabase.GetAssetPath(colorMap.texture);

            //Saving a non-persistent color map instance to disk
            if (textureAssetPath == string.Empty)
            {
                string colorMapAssetPath = AssetDatabase.GetAssetPath(colorMap);
                //Save next to the colormap asset file
                textureAssetPath = colorMapAssetPath.Replace("Colormap.asset", "Colormap Texture.png");
            }

            //Debug.Log($"Saving to texture at: {textureAssetPath}");

            //Saving the color map to an asset file, whilst no render was made
            if (!bakedTexture)
            {
                bakedTexture = new Texture2D(colorMap.texture.width, colorMap.texture.height, TextureFormat.ARGB32, false, true);
            }

            var bytes = bakedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(textureAssetPath, bytes);
            Object.DestroyImmediate(bakedTexture);

            AssetDatabase.Refresh();

            var tImporter = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (tImporter != null)
            {
                tImporter.wrapMode = TextureWrapMode.Clamp;
                tImporter.sRGBTexture = false;
                tImporter.maxTextureSize = 4096;
                
                tImporter.SaveAndReimport();
            }

            //Now load it in
            if (EditorUtility.IsPersistent(colorMap)) colorMap.texture = (Texture2D)AssetDatabase.LoadAssetAtPath(textureAssetPath, typeof(Texture2D));
            #endif
        }
       
        #endregion
        
        #region Utilities
        public static void ApplyUVFromTerrainBounds(GrassColorMap colorMap, GrassColorMapRenderer renderer)
        {
            colorMap.bounds = GetTerrainBounds(renderer.terrainObjects);
            colorMap.uv = BoundsToUV(renderer.colorMap.bounds);
        }
        
        public static Bounds GetTerrainBounds(List<GameObject> terrainObjects)
        {
            Vector3 minSum = Vector3.one * Mathf.Infinity;
            Vector3 maxSum = Vector3.one * Mathf.NegativeInfinity;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            
            foreach (GameObject item in terrainObjects)
            {
                if (item == null) continue;

                Terrain t = item.GetComponent<Terrain>();
                MeshRenderer r = t ? null : item.GetComponent<MeshRenderer>();

                if (t)
                {
                    //Min/max bounds corners in world-space
                    min = t.GetPosition(); //Doesn't exactly represent the minimum bounds value, but doesn't have to be
                    max = t.GetPosition() + t.terrainData.size; //Note, size is slightly more correct in height than bounds
                }

                if (r)
                {
                    //World-space bounds corners
                    min = r.bounds.min;
                    max = r.bounds.max;
                }
                
                minSum = Vector3.Min(minSum, min);
                
                //Must handle each axis separately, terrain may be further away, but not necessarily higher
                maxSum.x = Mathf.Max(maxSum.x, max.x);
                maxSum.y = Mathf.Max(maxSum.y, max.y);
                maxSum.z = Mathf.Max(maxSum.z, max.z);
            }

            Bounds b = new Bounds(Vector3.zero, Vector3.zero);

            b.SetMinMax(minSum, maxSum);

            //Increase bounds height for flat terrains by 1 unit up and down
            if (b.size.y < 2f)
            {
                b.Encapsulate(b.center + Vector3.up);
                b.Encapsulate(b.center + Vector3.down);
            }

            //Ensure bounds is always square
            b.size = new Vector3(Mathf.Max(b.size.x, b.size.z), b.size.y, Mathf.Max(b.size.x, b.size.z));
            b.center = Vector3.Lerp(b.min, b.max, 0.5f);

            return b;
        }
         
        public static Vector4 BoundsToUV(Bounds b)
        {
            Vector4 uv = new Vector4();

            //Origin position
            uv.x = b.min.x;
            uv.y = b.min.z;
            //Scale factor
            uv.z = 1f / b.size.x;
            uv.w = 0f;

            return uv;
        }
         
        public static double GetTexelSize(float texelSize, float worldSize)
        {
            return System.Math.Round(texelSize / worldSize, 2);
        }

        public static int IndexToResolution(int i)
        {
            int res = 0;

            switch (i)
            {
                case 0:
                    res = 64; break;
                case 1:
                    res = 128; break;
                case 2:
                    res = 256; break;
                case 3:
                    res = 512; break;
                case 4:
                    res = 1024; break;
                case 5:
                    res = 2048; break;
                case 6:
                    res = 4096; break;
            }

            return res;
        }

        public static int DetailPercentageToMipLevel(float percentage)
        {
            return Mathf.FloorToInt((100f - percentage) / 10);
        }
        
        //Create an RGBA component mask (eg. i=2 samples the Blue channel)
        public static Vector4 GetVectorMask(int i)
        {
            int index = i % 4;
            switch (index)
            {
                case 0: return new Vector4(1, 0, 0, 0);
                case 1: return new Vector4(0, 1, 0, 0);
                case 2: return new Vector4(0, 0, 1, 0);
                case 3: return new Vector4(0, 0, 0, 1);

                default: return Vector4.zero;
            }
        }

        //Returns the splatmap index for a given terrain layer
        public static int GetSplatmapID(int layerID)
        {
            if (layerID > 11) return 3;
            if (layerID > 7) return 2;
            if (layerID > 3) return 1;

            return 0;
        }
        #endregion
    }
}