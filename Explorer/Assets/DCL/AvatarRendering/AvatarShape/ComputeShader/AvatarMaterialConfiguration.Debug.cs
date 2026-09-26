using DCL.Diagnostics;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public partial class AvatarMaterialConfiguration
    {
        [Conditional("DCL_MATERIAL_DEBUGGING")]
        private static void DebugMaterial(Material originalMaterial, Material avatarMaterial, Renderer meshRenderer)
        {
            const bool B_LOCAL_KEYWORD_ORIGINAL_MAT_LOGGING = true;
            const bool B_LOCAL_KEYWORD_AVATAR_MAT_LOGGING = true;
            const bool B_GLOBAL_KEYWORD_LOGGING = true;
            const bool B_SHADER_VARIABLE_ORIGINAL_MAT_LOGGING = true;

            if (B_LOCAL_KEYWORD_ORIGINAL_MAT_LOGGING)
            {
                var outputShaderLocalKeywords_OriginalMaterial = "";

                foreach (LocalKeyword avatarMaterialShaderKeyword in originalMaterial.enabledKeywords)
                {
                    if (avatarMaterialShaderKeyword.isValid)
                        outputShaderLocalKeywords_OriginalMaterial += avatarMaterialShaderKeyword + " ";
                }

                ReportHub.Log(ReportCategory.AVATAR, $"For renderer {meshRenderer.name} the original material local keywords are {outputShaderLocalKeywords_OriginalMaterial}");
            }

            if (B_LOCAL_KEYWORD_AVATAR_MAT_LOGGING)
            {
                var outputShaderLocalKeywords_AvatarMaterial = "";

                foreach (LocalKeyword avatarMaterialShaderKeyword in avatarMaterial.enabledKeywords)
                {
                    if (avatarMaterialShaderKeyword.isValid)
                        outputShaderLocalKeywords_AvatarMaterial += avatarMaterialShaderKeyword + " ";
                }

                ReportHub.Log(ReportCategory.AVATAR, $"For renderer {meshRenderer.name} the avatar material local keywords are {outputShaderLocalKeywords_AvatarMaterial}");
            }

            if (B_GLOBAL_KEYWORD_LOGGING)
            {
                var outputShaderGlobalKeywords = "";

                foreach (GlobalKeyword avatarMaterialShaderKeyword in Shader.globalKeywords) { outputShaderGlobalKeywords += avatarMaterialShaderKeyword + " "; }

                ReportHub.Log(ReportCategory.AVATAR, $"For renderer {meshRenderer.name} the global keywords are {outputShaderGlobalKeywords}");
            }

            if (B_SHADER_VARIABLE_ORIGINAL_MAT_LOGGING)
            {
                var outputShaderVariables_OriginalMaterial = "";
                outputShaderVariables_OriginalMaterial += "_QueueOffset: " + originalMaterial.GetFloat("_QueueOffset") + " ";
                outputShaderVariables_OriginalMaterial += "_SrcBlend: " + originalMaterial.GetFloat("_SrcBlend") + " ";
                outputShaderVariables_OriginalMaterial += "_DstBlend: " + originalMaterial.GetFloat("_DstBlend") + " ";
                outputShaderVariables_OriginalMaterial += "_Blend: " + originalMaterial.GetFloat("_Blend") + " ";
                ReportHub.Log(ReportCategory.AVATAR, $"For renderer {meshRenderer.name} the shader variables for the original material are {outputShaderVariables_OriginalMaterial}");
            }
        }
    }
}
