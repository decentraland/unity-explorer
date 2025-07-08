using DCL.StylizedSkybox.Scripts;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkyboxController))]
public class SkyboxControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SkyboxController skyboxController = (SkyboxController)target;

        skyboxController.ForceSetTimeOfDay(EditorGUILayout.Slider("Time", skyboxController.CurrentTimeOfDay, 0, 1), SkyboxTimeSource.PLAYER_FIXED);
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("00:00"))
            {
                skyboxController.ForceSetTimeOfDay(0, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("06:00"))
            {
                skyboxController.ForceSetTimeOfDay(6*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("12:00"))
            {
                skyboxController.ForceSetTimeOfDay(12*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("18:00"))
            {
                skyboxController.ForceSetTimeOfDay(18*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("23:59"))
            {
                skyboxController.ForceSetTimeOfDay(24*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("\u25B6 Play"))
        {
            skyboxController.ForceSetDayCycleEnabled(true, SkyboxTimeSource.GLOBAL);
        }
        if (GUILayout.Button("\u25AE\u25AE Pause"))
        {
            skyboxController.ForceSetDayCycleEnabled(false, SkyboxTimeSource.PLAYER_FIXED);
        }
        GUILayout.EndHorizontal();

        DrawDefaultInspector();
    }
}
