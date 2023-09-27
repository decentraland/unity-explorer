using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class UnityCustomSkinning : CustomSkinning
    {
        public override void ComputeSkinning(NativeArray<float4x4> bonesResult)
        {

        }

        public override int Initialize(List<GameObject> gameObjects, Transform[] bones, TextureArrayContainer textureArrayContainer, UnityEngine.ComputeShader skinningShader, Material avatarMaterial,
            int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Transform rootTransform = gameObject.transform;

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    ResetTransforms(skinnedMeshRenderer, rootTransform);
                    skinnedMeshRenderer.bones = baseAvatarSkinnedMeshRenderer.bones;
                    skinnedMeshRenderer.rootBone = baseAvatarSkinnedMeshRenderer.rootBone;
                    SetupMaterial(skinnedMeshRenderer,0,textureArrayContainer,avatarMaterial,0);
                }
            }
            return 0;
        }

        protected override void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, Material celShadingMaterial, int lastAvatarVertCount)
        {
            var vertOutMaterial = new Material(celShadingMaterial);

            var albedoTexture = (Texture2D)meshRenderer.material.mainTexture;

            if (albedoTexture != null)
            {
                UsedTextureArraySlot usedIndex = textureArrayContainer.SetTexture(vertOutMaterial, albedoTexture, ComputeShaderHelpers.TextureArrayType.ALBEDO);
                //usedTextureArraySlots.Add(usedIndex);
            }

            foreach (string keyword in ComputeShaderHelpers.keywordsToCheck)
            {
                if (meshRenderer.material.IsKeywordEnabled(keyword))
                    vertOutMaterial.EnableKeyword(keyword);
            }

            //vertOutMaterial.SetColor(ComputeShaderHelpers._BaseColour_ShaderID, Color.red);
            meshRenderer.material = vertOutMaterial;
            vertOutMaterial.SetInteger("_useCompute", 0);
        }

        public void Dispose()
        {
        }
    }
}
