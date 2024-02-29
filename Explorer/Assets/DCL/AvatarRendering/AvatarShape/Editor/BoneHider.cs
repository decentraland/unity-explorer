using UnityEditor;
using UnityEngine.Animations.Rigging;
using UnityEditor.SceneManagement;
using ScriptableWizard = UnityEditor.ScriptableWizard;

public class BoneHider : ScriptableWizard
{
    [MenuItem("Animation Rigging/Toggle Bones")]
    public static void ToggleBones()
    {
        BoneRenderer[] bones = FindObjectsOfType<BoneRenderer>();
        foreach (var bone in bones)
        {
            bone.enabled = !bone.enabled;
        }
    }
 
    [MenuItem("Animation Rigging/Toggle Effectors")]
    public static void ToggleEffectors()
    {
        Rig[] rigs = FindObjectsOfType<Rig>();
        foreach (var rig in rigs)
        {
            var enumerator = rig.effectors.GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumerator.Current.visible = !enumerator.Current.visible;
            }
        }
    }
}
