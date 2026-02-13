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
            Debug.Log("<color=cyan>VFX Started</color>");
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        VisualEffect vfx = animator.GetComponentInChildren<VisualEffect>();
        if (vfx != null)
        {
            vfx.SendEvent("OnGliderEnd");
            Debug.Log("<color=orange>VFX Stopped</color>");
        }
    }
}