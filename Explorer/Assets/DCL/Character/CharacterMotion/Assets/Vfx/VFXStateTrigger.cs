using UnityEngine;
using UnityEngine.VFX;

namespace DCL.CharacterMotion.Vfx
{
    public class VFXStateTrigger : StateMachineBehaviour
    {
        private static readonly int GLIDER_START_EVENT = Shader.PropertyToID("OnGliderStart");
        private static readonly int GLIDER_END_EVENT = Shader.PropertyToID("OnGliderEnd");

        private VisualEffect? vfx;

        public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash) =>
            vfx = animator.GetComponentInChildren<VisualEffect>();

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (vfx != null) vfx.SendEvent(GLIDER_START_EVENT);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (vfx != null) vfx.SendEvent(GLIDER_END_EVENT);
        }
    }
}
