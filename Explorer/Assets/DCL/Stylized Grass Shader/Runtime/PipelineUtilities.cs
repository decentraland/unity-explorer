//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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
    public static class PipelineUtilities
    {
        private const string renderDataListFieldName = "m_RendererDataList";
        
#if URP
        /// <summary>
        /// Checks if a ForwardRenderer has been assigned to the pipeline asset, if not it is added
        /// </summary>
        /// <param name="pass"></param>
        public static void ValidatePipelineRenderers(ScriptableRendererData pass)
        {
            if (pass == null)
            {
                Debug.LogError("Pass is null");
                return;
            }
            
            BindingFlags bindings = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            ScriptableRendererData[] m_rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, bindings).GetValue(UniversalRenderPipeline.asset);
            bool isPresent = false;
            
            for (int i = 0; i < m_rendererDataList.Length; i++)
            {
                if (m_rendererDataList[i] == pass) isPresent = true;
            }

            if (!isPresent)
            {
                AddRendererToPipeline(pass);
            }
            else
            {
                #if SGS_DEV
                Debug.Log($"The {pass.name} ScriptableRendererFeature is already assigned to the pipeline asset");
                #endif
            }
        }
        
        private static void AddRendererToPipeline(ScriptableRendererData pass)
        {
            if (pass == null) return;

            BindingFlags bindings = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            ScriptableRendererData[] m_rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, bindings).GetValue(UniversalRenderPipeline.asset);
            List<ScriptableRendererData> rendererDataList = new List<ScriptableRendererData>(m_rendererDataList);
            
            rendererDataList.Add(pass);
            
            #if SGS_DEV
            //Debug.Log($"Added {pass.name} ScriptableRendererFeature to the main pipeline asset");
            #endif
            
            typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, bindings).SetValue(UniversalRenderPipeline.asset, rendererDataList.ToArray());
        }

        public static void RemoveRendererFromPipeline(ScriptableRendererData pass)
        {
            if (pass == null)
            {
                Debug.Log("Pass provided is null");
                return;
            }
            
            BindingFlags bindings = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            ScriptableRendererData[] m_rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, bindings).GetValue(UniversalRenderPipeline.asset);
            List<ScriptableRendererData> rendererDataList = new List<ScriptableRendererData>(m_rendererDataList);

            if (rendererDataList.Contains(pass))
            {
                rendererDataList.Remove((pass));
                
                #if SGS_DEV
                //Debug.Log($"Removed {pass.name} ScriptableRendererFeature from the main pipeline asset");
                #endif
            }
            
            typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, bindings).SetValue(UniversalRenderPipeline.asset, rendererDataList.ToArray());
        }
        
        private static int GetDefaultRendererIndex(UniversalRenderPipelineAsset asset)
        {
            return (int)typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asset);;
        }
        
        /// <summary>
        /// Gets the renderer from the current pipeline asset that's marked as default
        /// </summary>
        /// <returns></returns>
        public static ScriptableRendererData GetDefaultRenderer()
        {
            if (UniversalRenderPipeline.asset)
            {
                ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
                    .GetField(renderDataListFieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(UniversalRenderPipeline.asset);
                int defaultRendererIndex = GetDefaultRendererIndex(UniversalRenderPipeline.asset);

                return rendererDataList[defaultRendererIndex];
            }
            else
            {
                Debug.LogError("No Universal Render Pipeline is currently active.");
                return null;
            }
        }

        /// <summary>
        /// Create an empty renderer, without any render features, but otherwise suitable for camera rendering
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static UniversalRendererData CreateEmptyRenderer(string name = "")
        {
            ScriptableRendererData defaultRenderer = GetDefaultRenderer();
            
            UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = name;
            
            UniversalRendererData r = (UniversalRendererData)defaultRenderer;
            
            //Need to copy these references, otherwise calling Camera.Render() with this renderer results in null-refs on the shaders in these resources.
            rendererData.shaders = r.shaders;
            rendererData.postProcessData = r.postProcessData;
            
            #if UNITY_2021_2_OR_NEWER
            rendererData.debugShaders = r.debugShaders;
            rendererData.xrSystemData = r.xrSystemData;
            #endif

            return rendererData;
        }
        
        /// <summary>
        /// Checks if a ScriptableRendererFeature is added to the default renderer
        /// </summary>
        /// <param name="addIfMissing"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool RenderFeatureAdded<T>(bool addIfMissing = false)
        {
            ScriptableRendererData forwardRenderer = GetDefaultRenderer();
            bool isPresent = false;

            foreach (ScriptableRendererFeature feature in forwardRenderer.rendererFeatures)
            {
                if(feature == null) continue;
                
                if (feature.GetType() == typeof(T)) isPresent = true;
            }
            
            if(!isPresent && addIfMissing) AddRenderFeature<T>(forwardRenderer);
            
            return isPresent;
        }
        
        /// <summary>
        /// Adds a ScriptableRendererFeature to the renderer (default is none is supplied)
        /// </summary>
        /// <param name="forwardRenderer"></param>
        /// <typeparam name="T"></typeparam>
        public static void AddRenderFeature<T>(ScriptableRendererData forwardRenderer = null, bool persistent = true)
        {
            if (forwardRenderer == null) forwardRenderer = GetDefaultRenderer();
            
            ScriptableRendererFeature feature = (ScriptableRendererFeature)ScriptableRendererFeature.CreateInstance(typeof(T).ToString());
            feature.name = typeof(T).ToString();
            
            //Add component https://github.com/Unity-Technologies/Graphics/blob/d0473769091ff202422ad13b7b764c7b6a7ef0be/com.unity.render-pipelines.universal/Editor/ScriptableRendererDataEditor.cs#L180
#if UNITY_EDITOR
            if (persistent)
            {
                AssetDatabase.AddObjectToAsset(feature, forwardRenderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out var guid, out long localId);
            }
#endif

            //Get feature list
            FieldInfo renderFeaturesInfo = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ScriptableRendererFeature> m_RendererFeatures = (List<ScriptableRendererFeature>)renderFeaturesInfo.GetValue(forwardRenderer);

            //Modify and set list
            m_RendererFeatures.Add(feature);
            renderFeaturesInfo.SetValue(forwardRenderer, m_RendererFeatures);

            if (persistent)
            {
                //Onvalidate will call ValidateRendererFeatures and update m_RendererPassMap
                MethodInfo validateInfo = typeof(ScriptableRendererData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                validateInfo.Invoke(forwardRenderer, null);

#if UNITY_EDITOR
                EditorUtility.SetDirty(forwardRenderer);
                AssetDatabase.SaveAssets();
#endif
            }

            if (persistent) Debug.Log("<b>" + feature.name + "</b> was added to the " + forwardRenderer.name + " renderer");
        }

        public static void AssignRendererToCamera(UniversalAdditionalCameraData camData, ScriptableRendererData pass)
        {
            if (UniversalRenderPipeline.asset)
            {
                if (pass)
                {
                    //list is internal, so perform reflection workaround
                    ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);

                    for (int i = 0; i < rendererDataList.Length; i++)
                    {
                        if (rendererDataList[i] == pass)
                        {
                            #if SGS_DEV
                            //Debug.Log($"Setting renderer \"{pass.name}\" (index:{i}) to {camData.gameObject.name}");
                            #endif
                            
                            camData.SetRenderer(i);
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("[StylizedGrassRenderer] No Universal Render Pipeline is currently active.");
            }
        }
#endif
    }
}
