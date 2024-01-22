using UnityEngine;

namespace StylizedGrass
{
    public class GrassColorMap : ScriptableObject
    {
        public Bounds bounds;
        public Vector4 uv;
        public Texture texture;
        public bool hasScalemap = false;
        
        [Tooltip("When enabled, a custom color map texture can be used")]
        public bool overrideTexture;
        public Texture2D customTex;

        public static GrassColorMap Active;

        private static readonly int _ColorMap = Shader.PropertyToID("_ColorMap"); 
        private static readonly int _ColorMapUV = Shader.PropertyToID("_ColorMapUV"); 
        private static readonly int _ColorMapParams = Shader.PropertyToID("_ColorMapParams");

        public static GrassColorMap CreateNew()
        {
            GrassColorMap newColorMap = ScriptableObject.CreateInstance<GrassColorMap>();
            
            #if UNITY_EDITOR
            string prefix =  UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;
            if (prefix == string.Empty) prefix = "Untitled";
            
            newColorMap.name = $"{prefix}_Colormap";
            #endif
            
            return newColorMap;
        }
        
        public void SetActive()
        {
            if (!texture || (overrideTexture && !customTex)) //Nothing rendered yet
            {
                return;
            }
            
            if ((overrideTexture && !customTex))
            {
                Debug.LogWarning("Tried to activate grass color map with null texture", this);
                return;
            }

            Shader.SetGlobalTexture(_ColorMap, overrideTexture ? customTex : texture);
            
            Shader.SetGlobalVector(_ColorMapUV, uv);
            Shader.SetGlobalVector(_ColorMapParams, new Vector4(1, hasScalemap ? 1 : 0, 0, 0));

            Active = this;
        }

        /// <summary>
        /// Disables sampling of a color map in the grass shader. This must be called when a color map was used, but the current game context no longer has one active
        /// </summary>
        public static void DisableGlobally()
        {
            Shader.SetGlobalTexture(_ColorMap, null);
            Shader.SetGlobalVector(_ColorMapUV, Vector4.zero);
            //Disables sampling of the color/scale map in the shader
            Shader.SetGlobalVector(_ColorMapParams, Vector4.zero);

            Active = null;
        }
    }
}