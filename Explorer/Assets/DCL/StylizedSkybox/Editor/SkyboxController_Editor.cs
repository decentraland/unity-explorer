using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkyboxController))]
public class SkyboxControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {

        SkyboxController skyboxController = (SkyboxController)target;

        skyboxController.SetTimeOverride(EditorGUILayout.Slider("Time", skyboxController.CurrentTimeNormalized, 0, 1));
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("00:00"))
            {
                skyboxController.SetTimeOverride(0);
            }
            if (GUILayout.Button("06:00"))
            {
                skyboxController.SetTimeOverride(6*60*60);
            }
            if (GUILayout.Button("12:00"))
            {
                skyboxController.SetTimeOverride(12*60*60);
            }
            if (GUILayout.Button("18:00"))
            {
                skyboxController.SetTimeOverride(18*60*60);
            }
            if (GUILayout.Button("23:59"))
            {
                skyboxController.SetTimeOverride(24*60*60);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("\u25B6 Play"))
        {
            skyboxController.UseDynamicTime = true;
        }
        if (GUILayout.Button("\u25AE\u25AE Pause"))
        {
            skyboxController.UseDynamicTime = false;
        }
        GUILayout.EndHorizontal();

        DrawDefaultInspector();
    }
}
