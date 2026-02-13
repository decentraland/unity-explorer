using UnityEngine;
using UnityEngine.VFX;

public class VFXStateTrigger : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        VisualEffect vfx = animator.GetComponentInChildren<VisualEffect>();
        if (vfx != null)
        {
            vfx.SendEvent("OnGliderStart");
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        VisualEffect vfx = animator.GetComponentInChildren<VisualEffect>();
        if (vfx != null)
        {
            vfx.SendEvent("OnGliderEnd");
        }
    }
}