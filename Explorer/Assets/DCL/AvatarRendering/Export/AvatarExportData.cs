using UnityEngine;

namespace DCL.AvatarRendering.Export
{
	public struct AvatarExportData
	{
		public GameObject UnmodifiedAvatar;
		public Vector3 ArmatureScale;

		public static AvatarExportData Create(GameObject unmodifiedAvatar, Vector3 armatureScale)
		{
			return new AvatarExportData
			{
				UnmodifiedAvatar = unmodifiedAvatar,
				ArmatureScale = armatureScale,
			};
		}
	}
}

