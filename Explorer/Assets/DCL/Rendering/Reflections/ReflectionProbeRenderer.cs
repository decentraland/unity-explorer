using UnityEngine;

public class ReflectionProbeRenderer : MonoBehaviour
{
    public float IntervalInSeconds = 5f;
    private ReflectionProbe reflectionProbe;
    private float timer;

    private void Start()
    {
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        reflectionProbe = GetComponent<ReflectionProbe>();
        timer = IntervalInSeconds;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Update the reflection probe after timer
        if (timer >= IntervalInSeconds)
        {
            _ = reflectionProbe.RenderProbe();
            RenderSettings.customReflectionTexture = reflectionProbe.texture;
            timer = 0f;
        }
    }
}
