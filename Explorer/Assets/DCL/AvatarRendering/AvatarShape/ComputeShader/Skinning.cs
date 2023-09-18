using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

public class Skinning : MonoBehaviour
{
    public ComputeShader cs;
    public ComputeBufferEvent onCreateOutBuffer;
    public DoubleComputeBufferEvent setSkinAndBone;
    [SerializeField] private Transform[] bones;

    private struct SVertInVBO
    {
        public Vector3 pos;
        public Vector3 norm;
        public Vector4 tang;
    }

    private struct SVertInSkin
    {
        public float weight0, weight1, weight2, weight3;
        public int index0, index1, index2, index3;
    }

    private struct SVertOut
    {
        public Vector3 pos;
        private Vector3 norm;
        private Vector4 tang;
    }

    private Mesh mesh;
    private int vertCount;
    private ComputeBuffer sourceVBO;
    private ComputeBuffer sourceSkin;
    private ComputeBuffer meshVertsOut;
    private ComputeBuffer mBones;

    private Matrix4x4[] boneMatrices;

    public SkinnedMeshRenderer baseBones;

    private SVertOut[] vertOutArray;
    private Vector3[] verticesForMesh;

    private MeshFilter filter;

    // Use this for initialization
    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        new[] { sourceVBO, sourceSkin, meshVertsOut, mBones }.ToList().ForEach(b => b.Dispose());
    }

    // Update is called once per frame
    private void Update()
    {
        SetBoneMatrices();
        ComputeSkinning();
        meshVertsOut.GetData(vertOutArray);

        for (var index = 0; index < vertOutArray.Length; index++)
            verticesForMesh[index] = vertOutArray[index].pos;

        filter.mesh.vertices = verticesForMesh;
    }

    private void SetBoneMatrices()
    {
        for (var i = 0; i < boneMatrices.Length; i++)
            boneMatrices[i] = transform.worldToLocalMatrix * bones[i].localToWorldMatrix * mesh.bindposes[i];

        mBones.SetData(boneMatrices);
    }

    private void ComputeSkinning()
    {
        int kernel = cs.FindKernel("main");
        cs.SetBuffer(kernel, "g_SourceVBO", sourceVBO);
        cs.SetBuffer(kernel, "g_SourceSkin", sourceSkin);
        cs.SetBuffer(kernel, "g_MeshVertsOut", meshVertsOut);
        cs.SetBuffer(kernel, "g_mBones", mBones);
        cs.SetInt("g_VertCount", vertCount);
        cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);
    }

    private void Initialize()
    {
        SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>();
        mesh = skin.sharedMesh;
        vertCount = mesh.vertexCount;
        vertOutArray = new SVertOut[vertCount];
        verticesForMesh = new Vector3[vertCount];

        SVertInVBO[] inVBO = Enumerable.Range(0, vertCount)
                                       .Select(
                                            idx => new SVertInVBO
                                            {
                                                pos = mesh.vertices[idx],
                                                norm = mesh.normals[idx],

                                                //tang = mesh.tangents[idx],
                                            })
                                       .ToArray();

        sourceVBO = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInVBO)));
        sourceVBO.SetData(inVBO);

        SVertInSkin[] inSkin = mesh.boneWeights.Select(
                                        w => new SVertInSkin
                                        {
                                            weight0 = w.weight0,
                                            weight1 = w.weight1,
                                            weight2 = w.weight2,
                                            weight3 = w.weight3,
                                            index0 = w.boneIndex0,
                                            index1 = w.boneIndex1,
                                            index2 = w.boneIndex2,
                                            index3 = w.boneIndex3,
                                        })
                                   .ToArray();

        sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInSkin)));
        sourceSkin.SetData(inSkin);

        meshVertsOut = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertOut)));
        onCreateOutBuffer.Invoke(meshVertsOut);

        bones = baseBones.bones;
        mBones = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));
        boneMatrices = bones.Select((b, idx) => transform.worldToLocalMatrix * b.localToWorldMatrix * mesh.bindposes[idx]).ToArray();

        setSkinAndBone.Invoke(sourceSkin, mBones);

        filter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        filter.mesh = skin.sharedMesh;
        meshRenderer.material = skin.material;

        var vertOutMaterial = new Material(Resources.Load<Material>("VertOutMaterial"));
        vertOutMaterial.mainTexture = meshRenderer.material.mainTexture;
        var mpb = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetBuffer("_VertIn", meshVertsOut);
        meshRenderer.SetPropertyBlock(mpb);

        meshRenderer.material = vertOutMaterial;

        Destroy(skin);
    }

    [Serializable]
    public class ComputeBufferEvent : UnityEvent<ComputeBuffer> { }

    [Serializable]
    public class DoubleComputeBufferEvent : UnityEvent<ComputeBuffer, ComputeBuffer> { }
}
