using DCL.AvatarRendering.AvatarShape.UnityInterface;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    public class AdditiveBreathBinder : AnimationJobBinder<AdditiveBreathJob, AdditiveBreathData>
    {
        public override AdditiveBreathJob Create(Animator animator, ref AdditiveBreathData data, Component component)
        {
            var job = new AdditiveBreathJob
            {
                upperArm = ReadWriteTransformHandle.Bind(animator, data.UpperArm),
                forearm = ReadWriteTransformHandle.Bind(animator, data.Forearm),
                hand = ReadWriteTransformHandle.Bind(animator, data.Hand),
                cachedRotations = data.Bridge.CachedRotations,
                bindPoseRotations = data.Bridge.BindPoseRotations,
            };

            return job;
        }

        public override void Destroy(AdditiveBreathJob job) { }
    }
}
