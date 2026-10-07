using System;
using System.Linq;
using Configurator;
using Data;
using JetBrains.Annotations;
using Preview;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Utils;

/// <summary>
/// Used for interacting with the unity renderer from JavaScript.
///
/// Reload must be called manually for the changes to take effect.
///
/// Usage: unityInstance.SendMessage('JSBridge', 'MethodName', 'value');
/// </summary>
public class JSBridge : MonoBehaviour
{
    // The request names a failure reply carries, so the page can match it to the call it made.
    private const string REQUEST_SCREENSHOT = "screenshot";
    private const string REQUEST_METRICS = "metrics";

    [SerializeField] private PreviewController previewController;
    [SerializeField] private ConfiguratorUIPresenter configuratorUIPresenter;

    [UsedImplicitly]
    public void ParseFromURL() => PreviewConfiguration.RecreateFrom(Application.absoluteURL);

    [UsedImplicitly]
    public void ParseFromString(string url) => PreviewConfiguration.RecreateFrom(url);

    [UsedImplicitly]
    public void SetMode(string value) => PreviewConfiguration.Instance.SetMode(value);

    [UsedImplicitly]
    public void SetType(string value) => PreviewConfiguration.Instance.SetType(value);

    [UsedImplicitly]
    public void SetProfile(string value) => PreviewConfiguration.Instance.Profile = value;

    [UsedImplicitly]
    public void SetEmote(string value) => PreviewConfiguration.Instance.Emote = value;

    [UsedImplicitly]
    public void AddBase64(string value) => PreviewConfiguration.Instance.AddBase64(value);

    [UsedImplicitly]
    public void ClearBase64() => PreviewConfiguration.Instance.Base64.Clear();

    [UsedImplicitly]
    public void SetUrns(string value) =>
        PreviewConfiguration.Instance.Urns = value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(URNUtils.SanitizeURN).ToList();

    [UsedImplicitly]
    public void SetBackground(string value) => PreviewConfiguration.Instance.SetBackground(value);

    [UsedImplicitly]
    public void SetShadow(string value) => PreviewConfiguration.Instance.SetShadow(value);

    [UsedImplicitly]
    public void SetGlow(string value) => PreviewConfiguration.Instance.SetGlow(value);

    [UsedImplicitly]
    public void SetSkinColor(string value) => PreviewConfiguration.Instance.SetSkinColor(value);

    [UsedImplicitly]
    public void SetHairColor(string value) => PreviewConfiguration.Instance.SetHairColor(value);

    [UsedImplicitly]
    public void SetEyeColor(string value) => PreviewConfiguration.Instance.SetEyeColor(value);

    [UsedImplicitly]
    public void SetBodyShape(string value) => PreviewConfiguration.Instance.BodyShape = value;

    [UsedImplicitly]
    public void SetShowAnimationReference(string value) => PreviewConfiguration.Instance.ShowAnimationReference = bool.Parse(value);

    [UsedImplicitly]
    public void SetProjection(string value) => PreviewConfiguration.Instance.Projection = value;

    [UsedImplicitly]
    public void SetContract(string value) => PreviewConfiguration.Instance.Contract = value;

    [UsedImplicitly]
    public void SetItemID(string value) => PreviewConfiguration.Instance.ItemID = value;

    [UsedImplicitly]
    public void SetTokenID(string value) => PreviewConfiguration.Instance.TokenID = value;

    [UsedImplicitly]
    public void SetDisableLoader(string value) => PreviewConfiguration.Instance.DisableLoader = bool.Parse(value);

    [UsedImplicitly]
    public void SetDisableSwitcher(string value) => PreviewConfiguration.Instance.DisableSwitcher = bool.Parse(value);

    [UsedImplicitly]
    public void SetUsername(string value) => PreviewConfiguration.Instance.Username = value;

    [UsedImplicitly]
    public void SetSpringBonesParams(string value)
    {
        SpringBones.SpringBonesParamsPayload payload;
        try { payload = SpringBones.SpringBonesParamsPayload.Parse(value); }
        catch (Exception e)
        {
            Debug.LogError($"[SpringBones] failed to parse SetSpringBonesParams payload: {e.Message}");
            return;
        }
        if (payload == null) return;
        previewController.SetSpringBonesParams(payload);
    }

    [UsedImplicitly]
    public void GetElementBounds(string elementName) => configuratorUIPresenter.GetElementBounds(elementName);

    [UsedImplicitly]
    public void GetEmoteLength() => NativeCalls.OnEmoteLength(previewController.GetEmoteLength());

    [UsedImplicitly]
    public void IsEmotePlaying() => NativeCalls.OnIsEmotePlaying(previewController.IsEmotePlaying());

    [UsedImplicitly]
    public void PlayEmote() => previewController.PlayEmote();

    [UsedImplicitly]
    public void PauseEmote() => previewController.PauseEmote();

    [UsedImplicitly]
    public void GoToEmote(string value) => previewController.GoToEmote(float.Parse(value));

    [UsedImplicitly]
    public void StopEmote() => previewController.StopEmote();

    [UsedImplicitly]
    public void EnableSound() => previewController.EnableSound();

    [UsedImplicitly]
    public void DisableSound() => previewController.DisableSound();

    [UsedImplicitly]
    public void HasSound() => NativeCalls.OnHasSound(previewController.HasSound());

    [UsedImplicitly]
    public void Reload() => previewController.InvokeReload();

    [UsedImplicitly]
    public void Cleanup() => previewController.Cleanup();

    [UsedImplicitly]
    public void SetHideControls(string value)
    {
        PreviewConfiguration.Instance.HideControls = bool.Parse(value);
        previewController.SetControlsHidden(PreviewConfiguration.Instance.HideControls);
    }

    /// <summary>
    /// Replies with the metrics of the items the caller asked to preview, or a request failure while a
    /// reload is in flight or nothing of theirs is loaded.
    /// </summary>
    [UsedImplicitly]
    public void GetMetrics()
    {
        if (previewController.IsLoading)
        {
            NativeCalls.OnRequestFailed(REQUEST_METRICS, "The preview is reloading");
            return;
        }

        var metrics = previewController.GetRequestedItemsMetrics(out var found);

        if (found == 0)
        {
            NativeCalls.OnRequestFailed(REQUEST_METRICS, "No requested item is loaded");
            return;
        }

        var payload = new MetricsPayload
        {
            triangles = metrics.Triangles,
            materials = metrics.Materials,
            textures = metrics.Textures,
            meshes = metrics.Meshes,
            bodies = metrics.Meshes,
            entities = found,
        };

        NativeCalls.OnMetrics(JsonUtility.ToJson(payload));
    }

    /// <summary>
    /// With no size, captures the canvas as it is on screen. With <c>width,height</c> in pixels, renders
    /// the view offscreen at exactly that size with no controls in it.
    /// </summary>
    [UsedImplicitly]
    public void TakeScreenshot(string size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            StartCoroutine(TakeScreenshotCoroutine());
            return;
        }

        var parts = size.Split(',');

        if (parts.Length != 2
            || !int.TryParse(parts[0], out var width) || width <= 0
            || !int.TryParse(parts[1], out var height) || height <= 0)
        {
            NativeCalls.OnRequestFailed(REQUEST_SCREENSHOT, $"Invalid screenshot size [{size}], expected width,height");
            return;
        }

        if (previewController.IsLoading)
        {
            NativeCalls.OnRequestFailed(REQUEST_SCREENSHOT, "The preview is reloading");
            return;
        }

        StartCoroutine(TakeSizedScreenshotAsync(width, height));
    }

    private async Awaitable TakeSizedScreenshotAsync(int width, int height)
    {
        string base64Png;

        try
        {
            base64Png = await previewController.CaptureScreenshotAsync(width, height);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to capture a {width}x{height} screenshot: {e.Message}");
            NativeCalls.OnRequestFailed(REQUEST_SCREENSHOT, e.Message);
            return;
        }

        NativeCalls.OnScreenshotTaken(base64Png);
    }

    private static async Awaitable TakeScreenshotCoroutine()
    {
        await Awaitable.EndOfFrameAsync();

        var width = Screen.width;
        var height = Screen.height;

        var rt = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.B8G8R8A8_UNorm);

        ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);

        var gpuReadbackRequest = await AsyncGPUReadback.RequestAsync(rt);

        if (gpuReadbackRequest.hasError)
        {
            Debug.LogError("Failed to capture screenshot");
            NativeCalls.OnRequestFailed(REQUEST_SCREENSHOT, "The GPU readback failed");
            return;
        }

        var sourceData = gpuReadbackRequest.GetData<byte>();

        var texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
        var destinationData = texture.GetRawTextureData<byte>();

        // We have to flip the pixels vertically because OpenGL reasons
        for (var i = 0; i < sourceData.Length; i += 4)
        {
            var arrayIndex = i / 4;
            var x = arrayIndex % width;
            var y = arrayIndex / width;
            var flippedY = (height - 1 - y);
            var flippedIndex = x + flippedY * width;

            destinationData[i] = sourceData[flippedIndex * 4];
            destinationData[i + 1] = sourceData[flippedIndex * 4 + 1];
            destinationData[i + 2] = sourceData[flippedIndex * 4 + 2];
            destinationData[i + 3] = sourceData[flippedIndex * 4 + 3];
        }

        ScreenshotCapture.RecoverStraightAlpha(destinationData);

        var pngBytes = texture.EncodeToPNG();
        var base64Png = Convert.ToBase64String(pngBytes);

        NativeCalls.OnScreenshotTaken(base64Png);

        RenderTexture.ReleaseTemporary(rt);
    }

    public static class NativeCalls
    {
#if UNITY_EDITOR
        // Saved under Recordings/ (gitignored) so the capture can be opened: the page that would
        // receive it does not exist in the Editor.
        public static void OnScreenshotTaken(string base64Str)
        {
            var folder = System.IO.Path.Combine(Application.dataPath, "..", "Recordings");
            System.IO.Directory.CreateDirectory(folder);
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder,
                $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
            System.IO.File.WriteAllBytes(path, Convert.FromBase64String(base64Str));
            Debug.Log($"NativeCall OnScreenshotTaken({base64Str.Length} bytes) saved to {path}");
        }

        public static void OnLoadComplete() => Debug.Log("NativeCall OnLoadComplete");

        public static void OnError(string message) => Debug.LogError($"NativeCall OnError({message})");

        public static void OnCustomizationDone(string message) => Debug.Log($"NativeCall OnCustomizationDone({message})");

        public static void OnElementBounds(string json) => Debug.Log($"NativeCall OnElementBounds({json})");

        public static void OnAvatarCustomizationStep(int step) => Debug.Log($"NativeCall OnAvatarCustomizationStep({step})");

        public static void OnEmoteLength(float length) => Debug.Log($"NativeCall OnEmoteLength({length})");

        public static void OnIsEmotePlaying(bool playing) => Debug.Log($"NativeCall OnIsEmotePlaying({playing})");

        public static void OnHasSound(bool hasSound) => Debug.Log($"NativeCall OnHasSound({hasSound})");

        public static void OnMetrics(string json) => Debug.Log($"NativeCall OnMetrics({json})");

        public static void OnRequestFailed(string request, string reason) =>
            Debug.LogWarning($"NativeCall OnRequestFailed({request}, {reason})");

        // ReSharper disable once InconsistentNaming
        public static void PreloadURLs(string urlsCSV) => Debug.Log($"NativeCall PreloadURLs({urlsCSV})");
#else
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnScreenshotTaken(string base64Str);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnLoadComplete();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnError(string message);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnCustomizationDone(string message);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnElementBounds(string json);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnAvatarCustomizationStep(int step);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnEmoteLength(float length);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnIsEmotePlaying(bool playing);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnHasSound(bool hasSound);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnMetrics(string json);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void OnRequestFailed(string request, string reason);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void PreloadURLs(string urlsCSV);
#endif
    }
}