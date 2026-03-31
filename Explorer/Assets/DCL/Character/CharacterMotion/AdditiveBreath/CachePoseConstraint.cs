using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Animation Rigging/Cache Pose Constraint")]
    public class CachePoseConstraint : RigConstraint<CachePoseJob, CachePoseData, CachePoseBinder>
    {
    }
}
