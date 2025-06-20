using DCL.StylizedSkybox.Scripts;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkyboxController))]
public class SkyboxControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SkyboxController skyboxController = (SkyboxController)target;

        skyboxController.SetTimeOfDay(EditorGUILayout.Slider("Time", skyboxController.CurrentTimeOfDay, 0, 1), SkyboxTimeSource.PLAYER_FIXED);
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("00:00"))
            {
                skyboxController.SetTimeOfDay(0, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("06:00"))
            {
                skyboxController.SetTimeOfDay(6*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("12:00"))
            {
                skyboxController.SetTimeOfDay(12*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("18:00"))
            {
                skyboxController.SetTimeOfDay(18*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
            if (GUILayout.Button("23:59"))
            {
                skyboxController.SetTimeOfDay(24*60*60, SkyboxTimeSource.PLAYER_FIXED);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("\u25B6 Play"))
        {
            skyboxController.SetDayNightCycleEnabled(true, SkyboxTimeSource.GLOBAL);
        }
        if (GUILayout.Button("\u25AE\u25AE Pause"))
        {
            skyboxController.SetDayNightCycleEnabled(false, SkyboxTimeSource.PLAYER_FIXED);
        }
        GUILayout.EndHorizontal();

        DrawDefaultInspector();
    }
}
