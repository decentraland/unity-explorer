using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.Character.CharacterMotion.AdditiveBreath
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Animation Rigging/Additive Breath Constraint")]
    public class AdditiveBreathConstraint : RigConstraint<AdditiveBreathJob, AdditiveBreathData, AdditiveBreathBinder>
    {
    }
}
