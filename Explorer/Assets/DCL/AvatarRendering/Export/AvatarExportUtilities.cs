using System.Collections.Generic;
using System.Linq;
using DCL.Diagnostics;
using UniHumanoid;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
	public static class AvatarExportUtilities
	{
		public static void EnforceDCLTPose(Dictionary<HumanBodyBones, Transform> bones)
		{
			// Reset all to identity first
			foreach (var bone in bones.Values)
				bone.localRotation = Quaternion.identity;

			// DCL-specific corrections
			if (bones.TryGetValue(HumanBodyBones.Hips, out var hips))
				hips.localRotation = Quaternion.Euler(-90, 0, -180);
            
			if (bones.TryGetValue(HumanBodyBones.Spine, out var spine))
				spine.localRotation = Quaternion.Euler(0, 0, -180);

			if (bones.TryGetValue(HumanBodyBones.LeftShoulder, out var leftShoulder))
				leftShoulder.localRotation = Quaternion.Euler(0, -180, -90);

			if (bones.TryGetValue(HumanBodyBones.RightShoulder, out var rightShoulder))
				rightShoulder.localRotation = Quaternion.Euler(0, 0, -90);

			if (bones.TryGetValue(HumanBodyBones.LeftFoot, out var leftFoot))
				leftFoot.localRotation = Quaternion.Euler(-90, 180, 0);

			if (bones.TryGetValue(HumanBodyBones.RightFoot, out var rightFoot))
				rightFoot.localRotation = Quaternion.Euler(-90, 180, 0);

			ReportHub.Log(ReportCategory.AVATAR_EXPORT,"Applied T-pose");
		}

		public static Mesh CloneAndRecalculateBounds(Mesh source)
		{
			var mesh = Object.Instantiate(source);
			mesh.name = source.name;
			mesh.RecalculateBounds();
			return mesh;
		}
		
		public static Avatar CreateHumanoidAvatar(ExportSkeletonMapping skeleton, Dictionary<HumanBodyBones, Transform> humanBones)
		{
			var animator = skeleton.Root.GetComponent<Animator>() ?? skeleton.Root.AddComponent<Animator>();

			var avatarDescription = AvatarDescription.Create();
			avatarDescription.SetHumanBones(humanBones);

			var avatar = avatarDescription.CreateAvatar(skeleton.Root.transform);

			if (avatar.isValid)
			{
				avatar.name = "DCL_Export_Avatar";
				animator.avatar = avatar;
			}
			else
			{
				ReportHub.LogError(ReportCategory.AVATAR_EXPORT,"Avatar is not valid!");
				return null;
			}

			return avatar;
		}
	}
}