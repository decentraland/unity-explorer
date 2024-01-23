//Stylized Grass Shader
//Staggart Creations (http://staggart.xyz)
//Copyright protected under Unity Asset Store EULA

#if SGS_DEV
#define ENABLE_SHADER_STRIPPING_LOG
//Deep debugging only, makes the stripping process A LOT slower
//#define ENABLE_KEYWORD_STRIPPING_LOG
#endif

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
#if URP
using UnityEngine.Rendering.Universal;
#endif

namespace StylizedGrass
{
    public class ShaderConfigurator
    {
        public enum Integration
        {
            None,
            [InspectorName("Vegetation Studio (Pro)")]
            VegetationStudio,
            [InspectorName("Nature Renderer")]
            NatureRenderer,
            [InspectorName("Nature Renderer 2021")]
            NatureRenderer2021,
            [InspectorName("GPU Instancer")]
            GPUInstancer
        }

        private const string VegetationStudioGUID = "a9324aff8d6fb7746847dbf6108e0382";
        private const string NatureRendererGUID = "e184c5532d8acad44a76e8763685710f";
        private const string NatureRenderer2021GUID = "ca4c4574fc8ceab448f85800842a6cee";
        private const string GPUInstancerGUID = "18df6f4b5f1ec6045ad24ed3cf05d13b";

        public static Integration CurrentIntegration
        {
            get { return (Integration)EditorPrefs.GetInt(PlayerSettings.productName + "_SGS_SHADER_INTEGRATION", 0); }
            set { EditorPrefs.SetInt(PlayerSettings.productName + "_SGS_SHADER_INTEGRATION", (int)value); }
        }

        private const string ShaderGUID = "d7dd1c3f4cba1d441a7d295a168bac0d";
        private static string ShaderFilePath;

        private static void RefreshShaderFilePath()
        {
            ShaderFilePath = AssetDatabase.GUIDToAssetPath(ShaderGUID);
        }

        public static void SetIntegration(Integration integration)
        {
            RefreshShaderFilePath();

            EditorUtility.DisplayProgressBar(AssetInfo.ASSET_NAME, "Modifying shader...", 1f);
            {
                ToggleCodeBlock(ShaderFilePath, Integration.None.ToString(), integration == Integration.None);
                ToggleCodeBlock(ShaderFilePath, Integration.VegetationStudio.ToString(), integration == Integration.VegetationStudio);
                ToggleCodeBlock(ShaderFilePath, Integration.NatureRenderer.ToString(), integration == Integration.NatureRenderer);
                ToggleCodeBlock(ShaderFilePath, Integration.NatureRenderer2021.ToString(), integration == Integration.NatureRenderer2021);
                ToggleCodeBlock(ShaderFilePath, Integration.GPUInstancer.ToString(), integration == Integration.GPUInstancer);
            }
            
            SetIncludePath(integration);

            EditorUtility.ClearProgressBar();
            
            AssetDatabase.ImportAsset(ShaderFilePath);
            
            Debug.Log("Shader file modified to use " + integration.ToString() + " integration");

            CurrentIntegration = integration;
        }

        private struct CodeBlock
        {
            public int startLine;
            public int endLine;
        }
        
        private static void ToggleCodeBlock(string filePath, string id, bool enable)
        {
            string[] lines = File.ReadAllLines(filePath);

            List<CodeBlock> codeBlocks = new List<CodeBlock>();

            //Find start and end line indices
            for (int i = 0; i < lines.Length; i++)
            {
                bool blockEndReached = false;

                if (lines[i].StartsWith("/* Integration: ") && enable)
                {
                    lines[i] = $"/* Integration: {id} */";
                }

                if (lines[i].EndsWith($"/* start {id} */"))
                {
                    CodeBlock codeBlock = new CodeBlock();

                    codeBlock.startLine = i;

                    //Find related end point
                    for (int l = codeBlock.startLine; l < lines.Length; l++)
                    {
                        if (blockEndReached == false)
                        {
                            if (lines[l].EndsWith($"/* end {id} */"))
                            {
                                codeBlock.endLine = l;

                                blockEndReached = true;
                            }
                        }
                    }

                    codeBlocks.Add(codeBlock);
                    blockEndReached = false;
                }
            }

            if (codeBlocks.Count == 0)
            {
                //Debug.Log("No code blocks with the marker \"" + id + "\" were found in file");
            }

            foreach (CodeBlock codeBlock in codeBlocks)
            {
                if (codeBlock.startLine == codeBlock.endLine) continue;

                //Debug.Log((enable ? "Enabled" : "Disabled") + " \"" + id + "\" code block. Lines " + (codeBlock.startLine + 1) + " through " + (codeBlock.endLine + 1));

                for (int i = codeBlock.startLine + 1; i < codeBlock.endLine; i++)
                {
                    //Uncomment lines
                    if (enable == true)
                    {
                        if (lines[i].StartsWith("//") == true) lines[i] = lines[i].Remove(0, 2);
                    }
                    //Comment out lines
                    else
                    {
                        if (lines[i].StartsWith("//") == false) lines[i] = "//" + lines[i];
                    }
                }
            }

            File.WriteAllLines(filePath, lines);
        }

        private static void SetIncludePath(Integration config)
        {
            if (config == Integration.None) return;
            
            string GUID = string.Empty;
            
            switch (config)
            {
                case Integration.VegetationStudio: GUID = VegetationStudioGUID;
                    break;
                case Integration.NatureRenderer: GUID = NatureRendererGUID;
                    break;
                case Integration.NatureRenderer2021: GUID = NatureRenderer2021GUID;
                    break;
                case Integration.GPUInstancer: GUID = GPUInstancerGUID;
                    break;
                default: GUID = string.Empty;
                    break;
            }

            //Would be the case for default
            if (GUID == string.Empty) return;
            
            string libraryFilePath = AssetDatabase.GUIDToAssetPath(GUID);

            if (libraryFilePath == string.Empty)
            {
                if (EditorUtility.DisplayDialog(AssetInfo.ASSET_NAME,
                    config + " shader library could not be found with the GUID \"" + GUID + "\".\n\n" +
                    "This means it was changed by the author, you deleted the \".meta\" file at some point, or the asset simply isn't installed.", "Revert back", "Continue with errors"))
                {
                    SetIntegration(Integration.None);
                }
            }
            else
            {
                SetIncludePath(ShaderFilePath, config.ToString(), libraryFilePath);
            }
        }
        
        private static void SetIncludePath(string filePath, string id, string libraryPath)
        {
            string[] lines = File.ReadAllLines(filePath);

            //This assumes the line is already uncommented
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains($"/* include {id}"))
                {
                    lines[i + 1] = $"#include \"{libraryPath}\"";
                    
                    File.WriteAllLines(filePath, lines);
                }
            }
        }

        public static Integration GetIntegration(Shader shader)
        {
            string filePath = AssetDatabase.GetAssetPath(shader);

            string[] lines = File.ReadAllLines(filePath);

            string configStr = lines[0].Replace("/* Integration: ", string.Empty);
            configStr = configStr.Replace(" */", string.Empty);

            Integration config;
            Integration.TryParse(configStr, out config);

            CurrentIntegration = config;
            return config;
        }
    }
    
    #if URP
    //Strips shader variants for features belonging to a newer URP version. This avoids unnecessarily long build times in older URP versions
    class KeywordStripper : IPreprocessShaders
    {
        public int callbackOrder { get { return 0; } }
		private const string LOG_FILEPATH = "Library/Grass Shader Compilation.log";
		private const string SHADER_NAME = "Universal Render Pipeline/Nature/Stylized Grass";
        
        private List<ShaderKeyword> excludedKeywords;
        
        public KeywordStripper()
        {
            Initialize();   
        }

        private void Initialize()
        {
            //Note: Order in which keywords are declared should match the order in the passes
            excludedKeywords = new List<ShaderKeyword> 
            { 
                //new ShaderKeyword("DEBUG"),
                #if !UNITY_2020_2_OR_NEWER
                new ShaderKeyword("LIGHTMAP_SHADOW_MIXING"),
                new ShaderKeyword("SHADOWS_SHADOWMASK"),
                new ShaderKeyword("_SCREEN_SPACE_OCCLUSION"),
                #endif
				
				#if !UNITY_2021_2_OR_NEWER
				new ShaderKeyword("_MAIN_LIGHT_SHADOWS_SCREEN"),
				#endif

                #if !UNITY_2021_2_OR_NEWER
                new ShaderKeyword("_DBUFFER_MRT1"),
                new ShaderKeyword("_DBUFFER_MRT2"),
                new ShaderKeyword("_DBUFFER_MRT3"),
                new ShaderKeyword("_LIGHT_LAYERS"),
                new ShaderKeyword("_LIGHT_COOKIES"),
                //new ShaderKeyword("_RENDER_PASS_ENABLED"), //GBuffer only, so stripped anyway
                new ShaderKeyword("_CLUSTERED_RENDERING"),
                new ShaderKeyword("DYNAMICLIGHTMAP_ON"),
                new ShaderKeyword("DEBUG_DISPLAY"),
                #endif
                
                #if !UNITY_2022_2_OR_NEWER
                new ShaderKeyword("_FORWARD_PLUS"),
                new ShaderKeyword("_WRITE_RENDERING_LAYERS"),
                #endif
            };
			
			#if ENABLE_SHADER_STRIPPING_LOG
			//Clear log file
			File.WriteAllLines(LOG_FILEPATH, new string[] {});
			
			m_stripTimer = new Stopwatch();
			#endif
            
            #if SGS_DEV
            Debug.LogFormat("KeywordStripper initialized. {0} keywords are to be stripped", excludedKeywords.Count);
            #endif
        }

        #if ENABLE_SHADER_STRIPPING_LOG
        private System.Diagnostics.Stopwatch m_stripTimer;
        #endif
        
        //https://github.com/Unity-Technologies/Graphics/blob/9e934fb134d995259903b4850259c5c8953597f9/com.unity.render-pipelines.universal/Editor/ShaderPreprocessor.cs#L398
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> compilerDataList)
        {      
            if (compilerDataList == null || compilerDataList.Count == 0) return;

            if (shader.name != SHADER_NAME) return;

            if(excludedKeywords == null) Initialize();

			#if ENABLE_SHADER_STRIPPING_LOG
			File.AppendAllText(LOG_FILEPATH, $"\nOnProcessShader running for {shader.name}, Pass {snippet.passName}, (stage: {snippet.shaderType}). Num variants: {compilerDataList.Count}" + "\n" );

			m_stripTimer.Start();
			#endif

            if (StripUnusedPasses(shader, snippet))
            {
                compilerDataList.Clear();
            }
            
            for (int i = compilerDataList.Count -1; i >= 0; i--)
            {
				bool removeInput = false;
                removeInput = StripUnusedVariants(shader, compilerDataList[i], snippet);

                if (removeInput)
                {
                    compilerDataList.RemoveAt(i);
                    continue;
                }
            }
            
            #if ENABLE_SHADER_STRIPPING_LOG
            m_stripTimer.Stop();
			System.TimeSpan stripTimespan = m_stripTimer.Elapsed;
			File.AppendAllText(LOG_FILEPATH, $"OnProcessShader, stripping for pass {snippet.shaderType} took {stripTimespan.Minutes}m{stripTimespan.Seconds}s. Remaining variants to compile: {compilerDataList.Count}" + "\n" );
			m_stripTimer.Reset();
            #endif
        }
        
        private bool StripAllUnused(Shader shader, ShaderCompilerData compilerData, ShaderSnippetData snippet)
        {
            if (StripUnusedPasses(shader, snippet))
            {
                return true;
            }

			foreach (ShaderKeyword keyword in excludedKeywords)
			{
				if (StripKeyword(shader, compilerData, keyword, snippet))
				{
					return true;
				}
			}
			
            return false;
        }

        private bool StripUnusedVariants(Shader shader, ShaderCompilerData compilerData, ShaderSnippetData snippet)
        {
            foreach (ShaderKeyword keyword in excludedKeywords)
            {
                if (StripKeyword(shader, compilerData, keyword, snippet))
                {
                    return true;
                }
            }
			
            return false;
        }
        
        private bool StripUnusedPasses(Shader shader, ShaderSnippetData snippet)
        {
            #if !UNITY_2020_2_OR_NEWER
            if (snippet.passName == "DepthNormals")
            {
				#if ENABLE_SHADER_STRIPPING_LOG
				File.AppendAllText(LOG_FILEPATH, $"Stripped {snippet.passName} pass, (stage: {snippet.shaderType}) from {shader.name}, it belongs to a newer URP version" + "\n" );
				#endif
                return true;
            }
            #endif

            #if !UNITY_2021_2_OR_NEWER //Starting from URP10, there is a GBuffer pass, but no way to enable deferred rendering until URP 12
            if (snippet.passName == "GBuffer")
            {
				#if ENABLE_SHADER_STRIPPING_LOG
				File.AppendAllText(LOG_FILEPATH, $"Stripped {snippet.passName} pass, (stage: {snippet.shaderType}) from {shader.name}, it belongs to a newer URP version" + "\n" );
				#endif
                return true;
            }
            #endif
            
            return false;
        }

        private string GetKeywordName(Shader shader, ShaderKeyword keyword)
        {
            #if UNITY_2021_2_OR_NEWER
			return keyword.name;
			#else
            return ShaderKeyword.GetKeywordName(shader, keyword);
			#endif
        }

        private bool StripKeyword(Shader shader, ShaderCompilerData compilerData, ShaderKeyword keyword,  ShaderSnippetData snippet)
        {
            if (compilerData.shaderKeywordSet.IsEnabled(keyword))
            {
				#if ENABLE_SHADER_STRIPPING_LOG && ENABLE_KEYWORD_STRIPPING_LOG
                File.AppendAllText(LOG_FILEPATH, "- " + $"Stripped {GetKeywordName(shader, keyword)} variant from pass {snippet.passName} (stage: {snippet.shaderType})" + "\n" );
				#endif                

                return true;
            }

            return false;
        }
    }
    #endif
}