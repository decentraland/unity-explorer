using System;
using System.Linq;
using Configurator;
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
    [SerializeField] private PreviewController previewController;
    [SerializeField] private ConfiguratorUIPresenter configuratorUIPresenter;

    [UsedImplicitly]
    public void ParseFromURL() => AangConfiguration.RecreateFrom(Application.absoluteURL);

    [UsedImplicitly]
    public void ParseFromString(string url) => AangConfiguration.RecreateFrom(url);

    [UsedImplicitly]
    public void SetMode(string value) => AangConfiguration.Instance.SetMode(value);

    [UsedImplicitly]
    public void SetType(string value) => AangConfiguration.Instance.SetType(value);

    [UsedImplicitly]
    public void SetProfile(string value) => AangConfiguration.Instance.Profile = value;

    [UsedImplicitly]
    public void SetEmote(string value) => AangConfiguration.Instance.Emote = value;

    [UsedImplicitly]
    public void AddBase64(string value) => AangConfiguration.Instance.AddBase64(value);

    [UsedImplicitly]
    public void ClearBase64() => AangConfiguration.Instance.Base64.Clear();

    [UsedImplicitly]
    public void SetUrns(string value) =>
        AangConfiguration.Instance.Urns = value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(URNUtils.SanitizeURN).ToList();

    [UsedImplicitly]
    public void SetBackground(string value) => AangConfiguration.Instance.SetBackground(value);

    [UsedImplicitly]
    public void SetSkinColor(string value) => AangConfiguration.Instance.SetSkinColor(value);

    [UsedImplicitly]
    public void SetHairColor(string value) => AangConfiguration.Instance.SetHairColor(value);

    [UsedImplicitly]
    public void SetEyeColor(string value) => AangConfiguration.Instance.SetEyeColor(value);

    [UsedImplicitly]
    public void SetBodyShape(string value) => AangConfiguration.Instance.BodyShape = value;

    [UsedImplicitly]
    public void SetShowAnimationReference(string value) => AangConfiguration.Instance.ShowAnimationReference = bool.Parse(value);

    [UsedImplicitly]
    public void SetProjection(string value) => AangConfiguration.Instance.Projection = value;

    [UsedImplicitly]
    public void SetContract(string value) => AangConfiguration.Instance.Contract = value;

    [UsedImplicitly]
    public void SetItemID(string value) => AangConfiguration.Instance.ItemID = value;

    [UsedImplicitly]
    public void SetTokenID(string value) => AangConfiguration.Instance.TokenID = value;

    [UsedImplicitly]
    public void SetDisableLoader(string value) => AangConfiguration.Instance.DisableLoader = bool.Parse(value);

    [UsedImplicitly]
    public void SetDisableSwitcher(string value) => AangConfiguration.Instance.DisableSwitcher = bool.Parse(value);

    [UsedImplicitly]
    public void SetUsername(string value) => AangConfiguration.Instance.Username = value;

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
    public void TakeScreenshot() => StartCoroutine(TakeScreenshotCoroutine());

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
            NativeCalls.OnScreenshotTaken(null);
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

        var pngBytes = texture.EncodeToPNG();
        var base64Png = Convert.ToBase64String(pngBytes);

        NativeCalls.OnScreenshotTaken(base64Png);

        RenderTexture.ReleaseTemporary(rt);
    }

    public static class NativeCalls
    {
#if UNITY_EDITOR
        public static void OnScreenshotTaken(string base64Str) =>
            Debug.Log($"NativeCall OnScreenshotTaken({base64Str.Length} bytes)");

        public static void OnLoadComplete() => Debug.Log("NativeCall OnLoadComplete");

        public static void OnError(string message) => Debug.LogError($"NativeCall OnError({message})");

        public static void OnCustomizationDone(string message) => Debug.Log($"NativeCall OnCustomizationDone({message})");

        public static void OnElementBounds(string json) => Debug.Log($"NativeCall OnElementBounds({json})");

        public static void OnAvatarCustomizationStep(int step) => Debug.Log($"NativeCall OnAvatarCustomizationStep({step})");

        public static void OnEmoteLength(float length) => Debug.Log($"NativeCall OnEmoteLength({length})");

        public static void OnIsEmotePlaying(bool playing) => Debug.Log($"NativeCall OnIsEmotePlaying({playing})");

        public static void OnHasSound(bool hasSound) => Debug.Log($"NativeCall OnHasSound({hasSound})");

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
        public static extern void PreloadURLs(string urlsCSV);
#endif
    }
}