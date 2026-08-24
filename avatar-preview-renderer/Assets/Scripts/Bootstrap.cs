using Configurator;
using GLTFast;
using Preview;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private Material baseMat;
    [SerializeField] private Material facialFeaturesMat;

    [SerializeField] private ConfiguratorController configuratorController;
    [SerializeField] private PreviewController previewController;

    [SerializeField, TextArea] private string debugUrl;

    private void Start()
    {
        // Common assets
        CommonAssets.AvatarMaterial = baseMat;
        CommonAssets.FacialFeaturesMaterial = facialFeaturesMat;

        var url = Application.isEditor ? debugUrl : Application.absoluteURL;
        AangConfiguration.RecreateFrom(url);

        // The preview is near-static, so we cap the frame rate to keep GPU/CPU cost low on
        // consumers. Defaults to 60, overridable via the fps URL parameter (e.g. 30) for
        // consumers that want to trade smoothness for lower cost. VSync must be disabled first:
        // while it is on (QualitySettings.vSyncCount != 0) Application.targetFrameRate is ignored
        // and rendering is tied to the display refresh (120Hz+ on ProMotion / high-refresh screens).
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = AangConfiguration.Instance.Fps;

        // Small viewports (e.g. marketplace thumbnails) get supersampled for extra quality since the
        // absolute shaded pixel count stays bounded (under 1600x1600 -> under 3200x3200). Both
        // dimensions must be under the threshold, not just one, so a wide-but-short viewport doesn't
        // get its large dimension doubled too.
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset
            && Screen.width < 1600 && Screen.height < 1600)
        {
            urpAsset.renderScale = 2f;
        }

        if (AangConfiguration.Instance.UninterruptedDeferAgent)
        {
            // Sets uninterrupted defer agent for fastest loading
            GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());
        }

        if (AangConfiguration.Instance.Mode == PreviewMode.Configurator)
        {
            configuratorController.gameObject.SetActive(true);
            previewController.gameObject.SetActive(false);
        }
        else
        {
            configuratorController.gameObject.SetActive(false);
            previewController.gameObject.SetActive(true);
        }
    }

    [ContextMenu("Reload")]
    private void Reload()
    {
        var bridge = FindAnyObjectByType<JSBridge>();
        bridge.Reload();
    }

    [ContextMenu("Set Name")]
    private void SetName()
    {
        var bridge = FindAnyObjectByType<JSBridge>();
        bridge.SetUsername("Miha");
    }

}
