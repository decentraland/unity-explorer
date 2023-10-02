using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class UnityCustomSkinning : CustomSkinning
    {
        public override void ComputeSkinning(NativeArray<float4x4> bonesResult)
        {

        }

        public override int Initialize(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer, UnityEngine.ComputeShader skinningShader, IObjectPool<Material> avatarMaterial,
            int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer, AvatarShapeComponent avatarShapeComponent)
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Transform rootTransform = gameObject.transform;

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    ResetTransforms(skinnedMeshRenderer, rootTransform);
                    skinnedMeshRenderer.bones = baseAvatarSkinnedMeshRenderer.bones;
                    skinnedMeshRenderer.rootBone = baseAvatarSkinnedMeshRenderer.rootBone;
                    SetupMaterial(skinnedMeshRenderer, 0, textureArrayContainer, avatarMaterial, 0, avatarShapeComponent);
                }
            }
            return 0;
        }

        protected override void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, IObjectPool<Material> celShadingMaterial, int lastAvatarVertCount,
            AvatarShapeComponent avatarShapeComponent)
        {
            Material avatarMaterial = celShadingMaterial.Get();
            Material originalMaterial = meshRenderer.material;
            var albedoTexture = (Texture2D)originalMaterial.mainTexture;

            if (albedoTexture != null)
            {
                UsedTextureArraySlot usedIndex = textureArrayContainer.SetTexture(avatarMaterial, albedoTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
                //usedTextureArraySlots.Add(usedIndex);
            }

            foreach (string keyword in ComputeShaderConstants.keywordsToCheck)
            {
                if (meshRenderer.material.IsKeywordEnabled(keyword))
                    avatarMaterial.EnableKeyword(keyword);
            }

            // HACK: We currently aren't using normal maps so we're just creating shading issues by using this variant.
            avatarMaterial.DisableKeyword("_NORMALMAP");

            //vertOutMaterial.SetColor(ComputeShaderHelpers._BaseColour_ShaderID, Color.red);
            avatarMaterial.SetInteger("_useCompute", 0);
            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;
        }

        public void Dispose()
        {
        }
    }
}
