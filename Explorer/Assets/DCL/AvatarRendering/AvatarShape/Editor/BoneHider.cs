using UnityEditor;
using UnityEngine.Animations.Rigging;
using ScriptableWizard = UnityEditor.ScriptableWizard;

public class BoneHider : ScriptableWizard
{
    [MenuItem("Animation Rigging/Toggle Bones")]
    public static void ToggleBones()
    {
        // Find all instances of BoneRenderer in the scene.
        BoneRenderer[] bones = FindObjectsOfType<BoneRenderer>();
    
        // Check if there are any bones to toggle. If not, simply return.
        if (bones.Length == 0) return;

        // Determine the new value to set based on the first bone's current state.
        bool valueToSet = !bones[0].enabled;

        // Apply the determined value to all bones.
        foreach (var bone in bones)
        {
            bone.enabled = valueToSet;
        }
    }
 
    [MenuItem("Animation Rigging/Toggle Effectors")]
    public static void ToggleEffectors()
    {
        // Find all instances of Rig in the scene.
        Rig[] rigs = FindObjectsOfType<Rig>();
    
        // Initialize a flag to indicate if the valueToSet has been determined.
        bool valueDetermined = false;
        bool valueToSet = false;

        foreach (var rig in rigs)
        {
            foreach (var effector in rig.effectors)
            {
                // Determine the valueToSet based on the first effector's current visible state.
                if (!valueDetermined)
                {
                    valueToSet = !effector.visible;
                    valueDetermined = true;
                }

                // Apply the determined value to all effectors.
                effector.visible = valueToSet;
            }
        }
    }
}
