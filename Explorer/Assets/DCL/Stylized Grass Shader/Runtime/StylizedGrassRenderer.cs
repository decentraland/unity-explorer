//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA
 
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
 
namespace StylizedGrass
{
    [ExecuteInEditMode]
    [AddComponentMenu("Stylized Grass/Stylized Grass Renderer")]
    public class StylizedGrassRenderer : MonoBehaviour
    {
        public static StylizedGrassRenderer Instance;
     
        public bool debug = false;
     
        public RenderTexture vectorRT;
 
        [Tooltip("When a color map is assigned, this will be set as the active color map.\n\nHaving the Color Map Renderer component present would not longer be required.")]
        public GrassColorMap colorMap;
        [Tooltip("When enabled the grass Ambient and Gust strength values are multiplied by the WindZone's Main value")]
        public bool listenToWindZone;
        public WindZone windZone;
     
        public static int _GrassBendCoords = Shader.PropertyToID("_GrassBendCoords");
        private static int _GlobalWindParams = Shader.PropertyToID("_GlobalWindParams");
        private static int _GlobalWindDirection = Shader.PropertyToID("_GlobalWindDirection");
        private static int _GlobalWindOffset = Shader.PropertyToID("_GlobalWindOffset");

        [Tooltip("Acts as a multiplier for the WindZone's \"Main\" parameter. Use this to account for any discrepancies with other wind-enabled shaders.")]
        public float windAmbientMultiplier = 1f;
        [Tooltip("Acts as a multiplier for the WindZone's \"Turbulence\" parameter. Use this to account for any discrepancies with other wind-enabled shaders.")]
        public float windGustMultiplier = 1f;
        
        public void OnEnable()
        {
            Instance = this;
         
#if UNITY_EDITOR
            UnityEditor.SceneView.duringSceneGui += OnSceneGUI;
#endif
         
            if (colorMap)
            {
                colorMap.SetActive();
            }
            else
            {
                if (!GrassColorMapRenderer.Instance) GrassColorMap.DisableGlobally();
            }
         
            #if UNITY_EDITOR && URP
            if (Application.isPlaying == false)
            {
                if (!PipelineUtilities.RenderFeatureAdded<GrassRenderFeature>())
                {
                    Debug.LogError("The \"Grass Render Feature\" hasn't been added to the render pipeline. Check the inspector for setup instructions", this);
                    UnityEditor.EditorGUIUtility.PingObject(this);
                }
            }
            #endif
        }

        public void OnDisable()
        {
            Instance = null;
 
            //Shader needs to disable texture reading, since default global textures are gray
            Shader.SetGlobalVector(_GrassBendCoords, Vector4.zero);
            //Disable usage of Wind Zone parameters
            Shader.SetGlobalVector(_GlobalWindParams, Vector4.zero);
 
#if UNITY_EDITOR
            UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
#endif
        }
     
        public static void SetWindZone(WindZone windZone)
        {
            if (!Instance)
            {
                Debug.LogWarning("Tried to set Stylized Grass Renderer wind zone, but no instance is present");
                return;
            }
 
            Instance.windZone = windZone;
        }
 
        private void Update()
        {
            UpdateWind();
        }
 
        private double lastFrameTime;
        private double timeOffset;
        private Vector3 windDirection;
        private Vector3 gustOffset;
        private static Vector4 globalWindParams;

        private float m_windMain;
        private float m_windTurbulence;
        
        private void UpdateWind()
        {
            if (listenToWindZone && windZone && windZone.gameObject.activeInHierarchy)
            {
                //This is used as a boolean in the wind shader function, to indicate that the values passed on here should be used.
                globalWindParams.w = 1;
                
                double deltaTime = Time.time - lastFrameTime;
                lastFrameTime = Time.time;

                //Apply normalization multipliers
                m_windMain = windZone.windMain * windAmbientMultiplier;
                m_windTurbulence = windZone.windTurbulence * windGustMultiplier;
                
                timeOffset += deltaTime * (double)m_windMain * Time.timeScale;

                globalWindParams.x = (float)timeOffset;
                //Not allowing negative values, as this reverses the direction
                globalWindParams.y = Mathf.Max(0, m_windMain);
                globalWindParams.z = Mathf.Max(0, m_windTurbulence);

                Shader.SetGlobalVector(_GlobalWindParams, globalWindParams);
                
                windDirection = windZone.transform.rotation * Vector3.forward;
                Shader.SetGlobalVector(_GlobalWindDirection, windDirection);
                
                gustOffset += windDirection * (m_windTurbulence * (float)deltaTime * Time.timeScale); 
                Shader.SetGlobalVector(_GlobalWindOffset, gustOffset);
            }
            else
            {
                //When the .W component is 0, the shader uses material parameters to control wind
                Shader.SetGlobalVector(_GlobalWindParams, Vector4.zero);
            }
        }
 
        private void OnDrawGizmosSelected()
        {
            #if URP
            GrassRenderFeature.RenderBendVectors.DrawOrthographicViewGizmo();
            #endif
        }
 
        private void OnDrawGizmos()
        {
            if (listenToWindZone && windZone && windZone.gameObject.activeInHierarchy)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(windZone.transform.position, windZone.transform.position + (windDirection * 5f));
            }
        }
 
 
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI() //Has unwanted overhead, so exclude from build
        {
            DrawDebugGUI(false);
        }
 
        void DrawDebugGUI(bool sceneView)
        {
            vectorRT = (RenderTexture)Shader.GetGlobalTexture("_GrassOffsetVectors");
 
            if (!vectorRT) return;
         
            Rect imgRect = new Rect(5, 5, 256, 256);
            //Set to UI debug image
            if (debug && !sceneView)
            {
                GUI.DrawTexture(imgRect, vectorRT);
            }
         
            #if UNITY_EDITOR
            if (debug && sceneView)
            {
                Handles.BeginGUI();
 
                GUILayout.BeginArea(imgRect);
 
                EditorGUI.DrawTextureTransparent(imgRect, vectorRT);
                GUILayout.EndArea();
                Handles.EndGUI();
            }
            #endif
        }
#endif
     
#if UNITY_EDITOR
        private void OnSceneGUI(SceneView sceneView)
        {
            DrawDebugGUI(true);
        }
#endif
    }
}