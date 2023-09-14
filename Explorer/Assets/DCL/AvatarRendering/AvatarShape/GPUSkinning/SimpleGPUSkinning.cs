using System.Collections.Generic;
using UnityEngine;

public class SimpleGPUSkinning
{
    public Transform[] bones;
    public Renderer meshRenderer;
    public Material skinningMaterial;

    private Matrix4x4[] boneMatrices;
    private static readonly int BONE_MATRICES = Shader.PropertyToID("_Matrices");
    private static readonly int BIND_POSES = Shader.PropertyToID("_BindPoses");
    private static readonly int RENDERER_WORLD_INVERSE = Shader.PropertyToID("_WorldInverse");

    private static readonly HashSet<Mesh> processedBindPoses = new ();

    /// <summary>
    ///     This must be done once per SkinnedMeshRenderer before animating.
    /// </summary>
    /// <param name="skr"></param>
    private static void ConfigureBindPoses(SkinnedMeshRenderer skr)
    {
        if (processedBindPoses.Contains(skr.sharedMesh))
            return;

        processedBindPoses.Add(skr.sharedMesh);

        int vertexCount = skr.sharedMesh.vertexCount;
        var bone01data = new Vector4[vertexCount];
        var bone23data = new Vector4[vertexCount];

        Debug.Log($"Configuring bind poses for bones... vertex count: {vertexCount}");

        BoneWeight[] boneWeights = skr.sharedMesh.boneWeights;

        for (var i = 0; i < vertexCount; i++)
        {
            BoneWeight boneWeight = boneWeights[i];
            bone01data[i].x = boneWeight.boneIndex0;
            bone01data[i].y = boneWeight.weight0;
            bone01data[i].z = boneWeight.boneIndex1;
            bone01data[i].w = boneWeight.weight1;

            bone23data[i].x = boneWeight.boneIndex2;
            bone23data[i].y = boneWeight.weight2;
            bone23data[i].z = boneWeight.boneIndex3;
            bone23data[i].w = boneWeight.weight3;
        }

        skr.sharedMesh.SetUVs(1, bone01data);
        skr.sharedMesh.SetUVs(2, bone23data);
    }

    public SimpleGPUSkinning(SkinnedMeshRenderer skr)
    {
        ConfigureBindPoses(skr);

        GameObject go = skr.gameObject;

        go.AddComponent<MeshFilter>().sharedMesh = skr.sharedMesh;

        meshRenderer = go.AddComponent<MeshRenderer>();

        skinningMaterial = new Material(Resources.Load<Material>("GPUSkinningMaterial"));

        Texture tex = skr.sharedMaterial.mainTexture;

        if (tex)
            skinningMaterial.SetTexture("_MainTex", tex);

        skinningMaterial.SetMatrixArray(BIND_POSES, skr.sharedMesh.bindposes);

        meshRenderer.sharedMaterial = skinningMaterial;

        Object.Destroy(skr);
    }

    public void UpdateSkin(Matrix4x4[] bonesMatrices)
    {
        skinningMaterial.SetMatrix(RENDERER_WORLD_INVERSE, meshRenderer.worldToLocalMatrix);
        skinningMaterial.SetMatrixArray(BONE_MATRICES, bonesMatrices);
    }
}
