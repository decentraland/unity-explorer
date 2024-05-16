using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkyboxController))]
public class SkyboxControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {

        SkyboxController skyboxController = (SkyboxController)target;

        skyboxController.SetTimeNormalized(EditorGUILayout.Slider("Time", skyboxController.NormalizedTime, 0, 1));
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("00:00"))
            {
                skyboxController.SetTime(0);
            }
            if (GUILayout.Button("06:00"))
            {
                skyboxController.SetTime(6*60*60);
            }
            if (GUILayout.Button("12:00"))
            {
                skyboxController.SetTime(12*60*60);
            }
            if (GUILayout.Button("18:00"))
            {
                skyboxController.SetTime(18*60*60);
            }
            if (GUILayout.Button("23:59"))
            {
                skyboxController.SetTime(24*60*60);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("\u25B6 Play"))
        {
            skyboxController.Play();
        }
        if (GUILayout.Button("\u25AE\u25AE Pause"))
        {
            skyboxController.Pause();
        }
        GUILayout.EndHorizontal();

        DrawDefaultInspector();
    }
}
