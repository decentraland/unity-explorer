using UnityEngine;

public class ReflectionProbeRenderer : MonoBehaviour
{

    [SerializeField] private float intervalInSeconds = 5f;
    [SerializeField] private ReflectionProbe reflectionProbe;
    private float timer;
    private int renderId;

    private void Start()
    {
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        timer = intervalInSeconds;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Update the reflection probe after timer
        if (timer >= intervalInSeconds)
        {
            if(renderId == 0)
            {
                renderId = reflectionProbe.RenderProbe();
            }

            if(reflectionProbe.IsFinishedRendering(renderId))
            {
                RenderSettings.customReflectionTexture = reflectionProbe.texture;
                timer = 0f;
                renderId = 0;
            }
        }
    }
}
