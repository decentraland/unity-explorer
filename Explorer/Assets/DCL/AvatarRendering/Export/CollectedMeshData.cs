using UnityEngine;

namespace DCL.AvatarRendering.Export
{
	public class CollectedMeshData
	{
		public string Name { get; set; }
		public Mesh SharedMesh { get; set; }
		public Material[] Materials { get; set; }
		public bool IsSkinnedMesh { get; set; }

		// For SkinnedMeshRenderer
		public Transform[] SourceBones { get; set; }
		public string[] SourceBoneNames { get; set; }
		public string RootBoneName { get; set; }
		public float[] BlendShapeWeights { get; set; }

		// For rigid meshes
		public string OriginalParentPath { get; set; }
	}
}