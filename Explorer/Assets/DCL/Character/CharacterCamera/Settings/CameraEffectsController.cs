using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DepthOfField = UnityEngine.Rendering.Universal.DepthOfField;

[RequireComponent(typeof(Camera))]
public class CameraEffectsController : MonoBehaviour
{
    public Volume globalVolume;
    public ColorAdjustments colorAdjustments;
    public DepthOfField depthOfField;

    [Header("Basic Color Adjustments")]
    [Range(-5f, 5f)] public float postExposure = 0f;
    [Range(-100f, 100f)] public float contrast = 0f;
    [Range(-100f, 100f)] public float saturation = 0f;
    [Range(-180f, 180f)] public float hueShift = 0f;

    [Header("Color Filter")]
    [Range(0f, 360f)] public float filterHue = 0f;
    [Range(0f, 100f)] public float filterSaturation = 0f;
    [Range(0f, 100f)] public float filterValue = 100f;  // Default to 100 for neutral effect
    [Range(0f, 100f)] public float filterIntensity = 0f;

    private Color filterColor = Color.white;

    [Header("Depth of Field")]
    public bool enableDOF = false;
    [Range(0.1f, 50f)] public float focusDistance = 10f;
    [Range(1f, 300f)] public float focalLength = 50f;  // Changed range to be more camera-like
    [Range(1f, 32f)] public float aperture = 5.6f;     // F-stops like in real cameras

    private const string AUTO_VOLUME_NAME = "Global Volume (Auto Gen)";

    [Header("Autofocus")]
    public bool enableAutofocus = false;
    public LayerMask autofocusLayers = -1;  // All layers by default
    [Range(1, 60)] public float autofocusUpdateRate = 4;    // Updates per second
    [Range(0.1f, 20f)] public float autofocusBlendSpeed = 5f;  // How smooth the focus transition is
    [Range(0.1f, 100f)] public float autofocusMaxDistance = 50f;
    [Range(0.01f, 0.3f)] public float autofocusAreaSize = 0.1f;  // Size of focus sampling area

    public Camera mainCamera;
    private float targetFocusDistance;

    private float autofocusTimer;
    private bool hasValidFocusTarget;

    void Start()
    {
        mainCamera = Camera.main;
        targetFocusDistance = focusDistance;
    }

    [ContextMenu("Cache")]
    public void Cache()
    {
        if (globalVolume == null)
        {
            globalVolume = GameObject.Find(AUTO_VOLUME_NAME)?.GetComponent<Volume>();

            if (globalVolume == null)
            {
                GameObject volumeObject = new GameObject(AUTO_VOLUME_NAME);
                globalVolume = volumeObject.AddComponent<Volume>();

                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                globalVolume.profile = profile;

                // Setup Color Adjustments with all parameters
                colorAdjustments = profile.Add<ColorAdjustments>();
                colorAdjustments.postExposure.Override(0f);
                colorAdjustments.contrast.Override(0f);
                colorAdjustments.saturation.Override(0f);
                colorAdjustments.hueShift.Override(0f);
                colorAdjustments.colorFilter.Override(Color.white);

                // Setup Depth of Field
                depthOfField = profile.Add<DepthOfField>();
                depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
                depthOfField.focusDistance.Override(10f);
                depthOfField.focalLength.Override(50f);
                depthOfField.aperture.Override(5.6f);

                globalVolume.isGlobal = true;
                return;
            }
        }

        globalVolume.profile.TryGet(out colorAdjustments);
        globalVolume.profile.TryGet(out depthOfField);
    }

    void OnDisable()
    {
        if (globalVolume != null && globalVolume.gameObject.name == AUTO_VOLUME_NAME)
        {
            DestroyImmediate(globalVolume.profile);
            DestroyImmediate(globalVolume.gameObject);
        }
    }

    void Update()
    {
        UpdateColorEffects();
        UpdateDepthOfField();
        UpdateAutofocus();
    }

    void UpdateColorEffects()
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = postExposure;
            colorAdjustments.contrast.value = contrast;
            colorAdjustments.saturation.value = saturation;
            colorAdjustments.hueShift.value = hueShift;

            // Convert HSV to RGB for the color filter
            filterColor = Color.HSVToRGB(
                filterHue / 360f,           // Hue is in 0-360 range, needs to be 0-1
                filterSaturation / 100f,    // Saturation is in 0-100 range, needs to be 0-1
                filterValue / 100f          // Value is in 0-100 range, needs to be 0-1
            );

            // Apply intensity by lerping between white (no effect) and the target color
            Color finalFilterColor = Color.Lerp(Color.white, filterColor, filterIntensity / 100f);
            colorAdjustments.colorFilter.value = finalFilterColor;
        }
    }

    void UpdateDepthOfField()
    {
        if (depthOfField != null)
        {
            depthOfField.active = enableDOF;

            if (enableDOF)
            {
                    depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
                    depthOfField.focusDistance.value = focusDistance;
                    depthOfField.focalLength.value = focalLength;
                    depthOfField.aperture.value = aperture;
            }
        }
    }

    void UpdateAutofocus()
    {
        if (!enableAutofocus || !enableDOF) return;

        autofocusTimer += Time.deltaTime;

        // Check for new focus target based on update rate
        if (autofocusTimer >= 1f / autofocusUpdateRate)
        {
            autofocusTimer = 0f;

            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, autofocusMaxDistance, autofocusLayers))
            {
                targetFocusDistance = hit.distance;
            }
        }

        // Smoothly blend current focus distance to target
        if (Mathf.Abs(focusDistance - targetFocusDistance) > 0.01f)
        {
            focusDistance = Mathf.Lerp(focusDistance, targetFocusDistance, Time.deltaTime * autofocusBlendSpeed);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!enableAutofocus || !enableDOF || !mainCamera) return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        // Draw the autofocus sampling area
        Vector3 center = Vector3.forward * targetFocusDistance;
        float size = autofocusAreaSize * targetFocusDistance * 2f; // Scale area with distance
        Gizmos.color = hasValidFocusTarget ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(size, size, 0.1f));

        // Draw focus distance
        Gizmos.DrawLine(Vector3.zero, center);

        // Draw the focus plane
        Vector3 up = Vector3.up * size;
        Vector3 right = Vector3.right * size;
        Gizmos.DrawLine(center - up, center + up);
        Gizmos.DrawLine(center - right, center + right);

        Gizmos.matrix = oldMatrix;
    }
}
