using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using Data;
using JetBrains.Annotations;
using Services;
using UnityEngine;
using Utils;

public class PreviewConfiguration
{
    /// <summary>
    /// The <see cref="Emote"/> value that plays the rig's own idle clip.
    /// </summary>
    public const string DEFAULT_EMOTE = "idle";

    /// <summary>
    /// The <see cref="Emote"/> value that plays none.
    /// </summary>
    public const string NO_EMOTE = "none";

    // Babylon's default wheel precision: one unit of wheel delta per 100 of it.
    public const float DEFAULT_WHEEL_PRECISION = 100f;

    public static PreviewConfiguration Instance { get; private set; } = new();

    private PreviewConfiguration()
    {
    }

    /// <summary>
    /// The mode in which the preview will run.
    /// </summary>
    public PreviewMode Mode { get; private set; } = PreviewMode.Profile;

    /// <summary>
    /// Converts the string value to the appropriate mode. Defaults to Marketplace.
    /// </summary>
    /// <param name="value"></param>
    public void SetMode(string value)
    {
        Mode = value switch
        {
            "profile" => PreviewMode.Profile,
            "jesus" => PreviewMode.Jesus,
            "authentication" => PreviewMode.Authentication,
            "builder" => PreviewMode.Builder,
            "configurator" => PreviewMode.Configurator,
            _ => PreviewMode.Marketplace
        };
    }

    /// <summary>
    /// Which view the preview opens in, for the modes that offer more than one (currently only
    /// Marketplace). Null means the caller expressed no preference, in which case the view the user
    /// last picked is used instead.
    /// </summary>
    public PreviewViewType? Type { get; private set; }

    /// <summary>
    /// Converts the string value to the appropriate view type. Anything else (including the texture
    /// type the JS wrapper uses to skip the canvas entirely) leaves it unset, meaning no preference.
    /// </summary>
    /// <param name="value"></param>
    public void SetType(string value)
    {
        Type = value switch
        {
            "avatar" => PreviewViewType.Avatar,
            "wearable" => PreviewViewType.Wearable,
            _ => null
        };
    }

    /// <summary>
    /// An ethereum address of a profile to load as the base avatar.
    /// It can be set to default or a numbered default profile like default15 to use a default profile.
    /// </summary>
    public string Profile { get; set; } = "default1";

    /// <summary>
    /// The emote that the avatar will play. Default value is idle, other possible values are:
    /// clap, dab, dance, fashion, fashion-2, fashion-3,fashion-4, love, money, fist-pump and head-explode.
    /// <see cref="NO_EMOTE"/> plays none, so the avatar stands in the rig's idle unless a requested
    /// item is an emote. <see cref="DisableDefaultEmotes"/> drops only the default: an emote named
    /// on its own still plays, as in the Babylon preview.
    /// </summary>
    public string Emote
    {
        get => DisableDefaultEmotes && _emote == DEFAULT_EMOTE ? NO_EMOTE : _emote;
        set => _emote = string.IsNullOrEmpty(value) ? DEFAULT_EMOTE : value;
    }

    private string _emote = DEFAULT_EMOTE;

    /// <summary>
    /// Drops only the default emote, as Babylon's option of the same name does: an emote named on its
    /// own still plays. Kept as its own flag so the parameter order never matters.
    /// </summary>
    public bool DisableDefaultEmotes { get; private set; }

    public void SetDisableDefaultEmotes(string value) =>
        DisableDefaultEmotes = ParseToggle(value, "disableDefaultEmotes", DisableDefaultEmotes);

    /// <summary>
    /// The base64 encoded GLB to load.
    /// </summary>
    public List<byte[]> Base64 { get; } = new();

    /// <summary>
    /// Converts the string base64 to a byte array and adds it to the list, adding padding if needed
    /// </summary>
    public void AddBase64(string value)
    {
        var sanitized = (value.Length % 4) switch
        {
            2 => value + "==",
            3 => value + "=",
            0 => value,
            _ => throw new FormatException("Invalid Base64 string")
        };

        Base64.Add(Convert.FromBase64String(sanitized));
    }

    /// <summary>
    /// A URN of a wearable or an emote to load. If it is a wearable, it will override anything loaded from a profile.
    /// </summary>
    public List<string> Urns { get; set; } = new();

    /// <summary>
    /// Sanitizes an URN and adds it to the list.
    /// </summary>
    public void AddURN(string urn)
    {
        Urns.Add(URNUtils.SanitizeURN(urn));
    }

    /// <summary>
    /// URLs of wearable or emote definitions the page hosts, fetched and treated as <see cref="Base64"/>.
    /// </summary>
    public IReadOnlyList<string> Urls => _urls;

    private readonly List<string> _urls = new();

    public void AddUrl(string url) => _urls.Add(url);

    public void ClearUrls() => _urls.Clear();

    /// <summary>
    /// Only the requested items are worn: no profile wearables and no base ones for the empty
    /// categories, as the Babylon preview's option of the same name.
    /// </summary>
    public bool DisableDefaultWearables { get; private set; }

    public void SetDisableDefaultWearables(string value) =>
        DisableDefaultWearables = ParseToggle(value, "disableDefaultWearables", DisableDefaultWearables);

    /// <summary>
    /// Hosts for the Catalyst and the Marketplace API, replacing the ones <c>env</c> picks; null
    /// keeps those.
    /// </summary>
    public string PeerUrl { get; private set; }

    public string MarketplaceServerUrl { get; private set; }

    public void SetPeerUrl(string value) => PeerUrl = NormalizeHost(value);

    public void SetMarketplaceServerUrl(string value) => MarketplaceServerUrl = NormalizeHost(value);

    // An empty value goes back to the default host; a trailing slash would double up in the paths.
    // Only a web address that paths can be appended to is a host, a prefix path included but no
    // query or fragment: anything else keeps the default, since every request is HTTP.
    private static string NormalizeHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var host = value.Trim().TrimEnd('/');

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
            return host;

        Debug.LogWarning($"Unknown host [{value}], keeping the default");
        return null;
    }

    /// <summary>
    /// The color of the background in HEX.
    /// </summary>
    public Color Background { get; private set; } = new(0, 0, 0, 0);

    /// <summary>
    /// Sets the background color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetBackground(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            Background = color;
        }
        else
        {
            Background = Color.black;
            Debug.LogError("Failed to parse background color");
        }
    }

    /// <summary>
    /// Whether the avatar casts a shadow onto the floor. On by default, so every existing caller gets
    /// one; a caller compositing the canvas over its own artwork can opt out with <c>shadow=off</c>.
    /// </summary>
    public bool Shadow { get; private set; } = true;

    public void SetShadow(string value) => Shadow = ParseToggle(value, "shadow", Shadow);

    /// <summary>
    /// Whether a soft pool of light is drawn on the floor under the avatar, so it reads as standing
    /// on a lit surface rather than floating. Brightens the canvas the same way
    /// <see cref="Shadow"/> darkens it, and is on by default for the same reason.
    /// </summary>
    public bool Glow { get; private set; } = true;

    public void SetGlow(string value) => Glow = ParseToggle(value, "glow", Glow);

    public void SetHideControls(string value) => HideControls = ParseToggle(value, "hideControls", HideControls);

    // Accepts both the documented `off` spelling and the bool spelling the other parameters use. An
    // unrecognised value keeps `current` rather than throwing: bool.Parse would abort RecreateFrom
    // mid-loop on a typo and drop every parameter after it.
    private static bool ParseToggle(string value, string name, bool current)
    {
        switch (value.ToLowerInvariant())
        {
            case "off" or "false" or "0" or "no": return false;
            case "on" or "true" or "1" or "yes" or "": return true;
            default:
                Debug.LogWarning($"Unknown {name} value [{value}], keeping {name} {(current ? "on" : "off")}");
                return current;
        }
    }

    /// <summary>
    /// The color of the skin in HEX.
    /// </summary>
    public Color? SkinColor { get; private set; }

    /// <summary>
    /// Sets the skin color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetSkinColor(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            SkinColor = color;
        }
        else
        {
            SkinColor = null;
            Debug.LogError("Failed to parse skin color");
        }
    }

    /// <summary>
    /// The color of the hair in HEX.
    /// </summary>
    public Color? HairColor { get; private set; }

    /// <summary>
    /// Sets the skin color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetHairColor(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            HairColor = color;
        }
        else
        {
            HairColor = null;
            Debug.LogError("Failed to parse hair color");
        }
    }

    /// <summary>
    /// The color of the eyes in HEX.
    /// </summary>
    public Color? EyeColor { get; private set; }

    /// <summary>
    /// Sets the skin color from a hex string. The string must not contain a leading #.
    /// </summary>
    public void SetEyeColor(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out var color))
        {
            EyeColor = color;
        }
        else
        {
            EyeColor = null;
            Debug.LogError("Failed to parse eye color");
        }
    }

    /// <summary>
    /// The body shape URN (urn:decentraland:off-chain:base-avatars:BaseMale or urn:decentraland:off-chain:base-avatars:BaseFemale)
    /// </summary>
    public string BodyShape { get; set; }

    /// <summary>
    /// Shows or hides the animation reference platform.
    /// </summary>
    public bool ShowAnimationReference { get; set; }

    /// <summary>
    /// If we're using orthographic projection.
    /// </summary>
    public string Projection { get; set; } = "perspective";

    /// <summary>
    /// The contract address of the wearable collection.
    /// </summary>
    public string Contract { get; set; }

    /// <summary>
    /// The id of the item in the collection.
    /// </summary>
    public string ItemID { get; set; }

    /// <summary>
    /// The id of the token (to preview a specific NFT).
    /// </summary>
    public string TokenID { get; set; }

    /// <summary>
    /// If the loading circle should be shown when loading content.
    /// </summary>
    public bool DisableLoader { get; set; }

    /// <summary>
    /// Hides the avatar / item switcher. Opt-in, so it stays visible for every existing caller. Meant
    /// for callers that pin the view with <see cref="Type"/> and don't want the user to change it.
    /// </summary>
    public bool DisableSwitcher { get; set; }

    /// <summary>
    /// Hides every in-canvas control (switcher, emote and sound buttons). Opt-in, for embedders that
    /// draw their own controls around the canvas or capture it for thumbnails. The loader is separate:
    /// see <see cref="DisableLoader"/>.
    /// </summary>
    public bool HideControls { get; private set; }

    /// <summary>
    /// The camera options of the Babylon preview, kept by name so an app frames both renderers with
    /// the same values. <see cref="ZoomLevel"/> is 0 to 100 (closest view), <see cref="WheelZoom"/>
    /// how far the wheel pulls back from it (1 = not at all), <see cref="WheelStart"/> where the
    /// view begins in that range (100 = closest). Unset zoom and wheel options leave the renderer's
    /// own wheel range around the fitted view.
    /// </summary>
    public float? ZoomLevel { get; private set; }

    public float? WheelZoom { get; private set; }

    public float WheelStart { get; private set; } = 50f;

    /// <summary>
    /// Static freezes the view: no rotation, zoom or pan, no auto-rotate, and the emote held on its
    /// first frame.
    /// </summary>
    public PreviewCameraKind Camera { get; private set; } = PreviewCameraKind.Interactive;

    /// <summary>
    /// Per-axis input locks, Babylon's names: alpha is the turntable, beta the tilt, radius the zoom.
    /// </summary>
    public bool LockAlpha { get; private set; }

    public bool LockBeta { get; private set; }

    public bool LockRadius { get; private set; }

    /// <summary>
    /// Whether right-drag pans the view. On by default, as the wrapper defaults it.
    /// </summary>
    public bool Panning { get; private set; } = true;

    public bool DisableAutoRotate { get; private set; }

    /// <summary>
    /// Idle turntable speed in radians per second, Babylon's unit and default.
    /// </summary>
    public float AutoRotateSpeed { get; private set; } = 0.2f;

    /// <summary>
    /// Where the view starts, in metres across and up the screen: the thumbnail editor's slider drives
    /// the vertical one. Replaces any right-drag pan.
    /// </summary>
    public Vector2 Offset { get; private set; }

    /// <summary>
    /// Draws the centred half of the canvas a square thumbnail keeps, for the Builder's thumbnail editor.
    /// </summary>
    public bool ShowThumbnailBoundaries { get; private set; }

    /// <summary>
    /// Draws the space an avatar's items have to fit in, a 2 m wide, 3 m tall cylinder on a grid
    /// floor, as the Babylon preview does for the Builder's curators. The same as
    /// <see cref="ShowAnimationReference"/>, under Babylon's name.
    /// </summary>
    public bool ShowSceneBoundaries { get; private set; }

    /// <summary>
    /// Depth of the offset, Babylon's <c>offsetZ</c>: positive moves the view back from the subject.
    /// </summary>
    public float OffsetDepth { get; private set; }

    /// <summary>
    /// Where the Babylon preview would put its camera, in its world: each axis left unset keeps that
    /// preview's default for the view, so only what the page changed turns or pulls back the view.
    /// </summary>
    public float? CameraX { get; private set; }

    public float? CameraY { get; private set; }

    public float? CameraZ { get; private set; }

    /// <summary>
    /// How far a wheel notch zooms, Babylon's unit: 100 is the default, a larger value zooms less.
    /// </summary>
    public float WheelPrecision { get; private set; } = DEFAULT_WHEEL_PRECISION;

    /// <summary>
    /// Extra zoom on top of every other zoom option: 2 shows the subject twice as large.
    /// </summary>
    public float ZoomScale { get; private set; } = 1f;

    /// <summary>
    /// Keeps the subject where it stands instead of bringing its middle onto the view axis, so the
    /// offset and camera options act from the subject's own origin, as they do in the Babylon preview.
    /// </summary>
    public bool DisableAutoCenter { get; private set; }

    /// <summary>
    /// The social emote animation to play: which clip each armature plays, its loop and its audio.
    /// Unset, a social emote plays one of its outcomes at random, as the Babylon preview does.
    /// </summary>
    [CanBeNull] public SocialEmoteAnimation SocialEmote { get; private set; }

    public void SetZoomLevel(string value) => ZoomLevel = ParseOptionalNumber(value, "zoom", ZoomLevel);

    public void SetWheelZoom(string value) => WheelZoom = ParseOptionalNumber(value, "wheelZoom", WheelZoom);

    public void SetWheelStart(string value) => WheelStart = ParseNumber(value, "wheelStart", WheelStart);

    // An unknown value keeps the current camera, for the same reason ParseToggle does.
    public void SetCamera(string value)
    {
        var trimmed = value.Trim();

        if (string.Equals(trimmed, "static", StringComparison.OrdinalIgnoreCase))
            Camera = PreviewCameraKind.Static;
        else if (string.Equals(trimmed, "interactive", StringComparison.OrdinalIgnoreCase))
            Camera = PreviewCameraKind.Interactive;
        else
            Debug.LogWarning($"Unknown camera value [{value}], keeping camera {Camera}");
    }

    public void SetLockAlpha(string value) => LockAlpha = ParseToggle(value, "lockAlpha", LockAlpha);

    public void SetLockBeta(string value) => LockBeta = ParseToggle(value, "lockBeta", LockBeta);

    public void SetLockRadius(string value) => LockRadius = ParseToggle(value, "lockRadius", LockRadius);

    public void SetPanning(string value) => Panning = ParseToggle(value, "panning", Panning);

    public void SetDisableAutoRotate(string value) =>
        DisableAutoRotate = ParseToggle(value, "disableAutoRotate", DisableAutoRotate);

    public void SetAutoRotateSpeed(string value) =>
        AutoRotateSpeed = ParseNumber(value, "autoRotateSpeed", AutoRotateSpeed);

    public void SetOffsetX(string value) => Offset = new Vector2(ParseNumber(value, "offsetX", Offset.x), Offset.y);

    public void SetOffsetY(string value) => Offset = new Vector2(Offset.x, ParseNumber(value, "offsetY", Offset.y));

    public void SetOffset(Vector2 offset) => Offset = offset;

    public void SetShowThumbnailBoundaries(string value) =>
        ShowThumbnailBoundaries = ParseToggle(value, "showThumbnailBoundaries", ShowThumbnailBoundaries);

    public void SetShowSceneBoundaries(string value) =>
        ShowSceneBoundaries = ParseToggle(value, "showSceneBoundaries", ShowSceneBoundaries);

    public void SetOffsetZ(string value) => OffsetDepth = ParseNumber(value, "offsetZ", OffsetDepth);

    public void SetOffsetDepth(float depth) => OffsetDepth = depth;

    public void SetCameraX(string value) => CameraX = ParseOptionalNumber(value, "cameraX", CameraX);

    public void SetCameraY(string value) => CameraY = ParseOptionalNumber(value, "cameraY", CameraY);

    public void SetCameraZ(string value) => CameraZ = ParseOptionalNumber(value, "cameraZ", CameraZ);

    // Babylon floors it at 1.
    public void SetWheelPrecision(string value) =>
        WheelPrecision = Mathf.Max(ParseNumber(value, "wheelPrecision", WheelPrecision), 1f);

    // Babylon takes anything but a positive number as 1.
    public void SetZoomScale(string value)
    {
        var scale = ParseNumber(value, "zoomScale", ZoomScale);
        ZoomScale = scale > 0f ? scale : 1f;
    }

    public void SetDisableAutoCenter(string value) =>
        DisableAutoCenter = ParseToggle(value, "disableAutoCenter", DisableAutoCenter);

    // An empty value goes back to a random outcome; an invalid one keeps the current animation.
    public void SetSocialEmote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SocialEmote = null;
            return;
        }

        var animation = SocialEmoteAnimation.FromJson(value);

        if (animation != null)
            SocialEmote = animation;
        else
            Debug.LogWarning($"Unknown socialEmote value [{value}], keeping the current one");
    }

    /// <summary>
    /// Parses a finite number the way the page wrote it, with a dot decimal separator whatever the
    /// locale. NaN and the infinities are rejected: a clamp passes them through and they would stick
    /// in a zoom or a pan.
    /// </summary>
    public static bool TryParseNumber(string value, out float number) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && float.IsFinite(number);

    // An unparsable value keeps `current`, for the same reason ParseToggle does.
    private static float ParseNumber(string value, string name, float current)
    {
        if (TryParseNumber(value, out var number)) return number;

        Debug.LogWarning($"Unknown {name} value [{value}], keeping {name} {current}");
        return current;
    }

    // An empty value unsets the option, so a page can go back to the renderer's own range.
    private static float? ParseOptionalNumber(string value, string name, float? current)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (TryParseNumber(value, out var number)) return number;

        Debug.LogWarning($"Unknown {name} value [{value}], keeping {name} {current}");
        return current;
    }

    /// <summary>
    /// If we should instruct the browser to pre-fetch certain files. On by default.
    /// </summary>
    public bool UseBrowserPreload { get; set; } = true;

    /// <summary>
    /// The username used in the avatar creator
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Shows or hides the FPS counter.
    /// </summary>
    public bool ShowFPS { get; set; }

    /// <summary>
    /// The target frame rate the renderer runs at. Defaults to 60; can be lowered (e.g. to 30) by
    /// consumers that want to trade smoothness for lower GPU/CPU cost.
    /// </summary>
    public int Fps { get; set; } = 60;

    /// <summary>
    /// If true we load individual items for an avatar one after another, instead of concurrently.
    /// </summary>
    public bool ConcurrentLoad { get; set; } = !Application.isMobilePlatform;

    /// <summary>
    /// If true we use the <see cref="GLTFast.UninterruptedDeferAgent"/> for loading GLB files. If false, the
    /// default frame time limit one is used. Using the uninterrupted agent will produce faster loads but might
    /// case FPS dips / crashes on mobile devices.
    /// </summary>
    public bool UninterruptedDeferAgent { get; set; } = !Application.isMobilePlatform;

    /// <summary>
    /// If true, facial features (eyes, eyebrows, mouth) will be hidden on the avatar.
    /// </summary>
    public bool DisableFace { get; private set; }

    public void SetDisableFace(string value) => DisableFace = ParseToggle(value, "disableFace", DisableFace);

    public static void RecreateFrom(string url)
    {
        Instance = new PreviewConfiguration();

        if (string.IsNullOrEmpty(url) || !url.Contains('?')) return;

        var split = url[(url.IndexOf('?') + 1)..].Split('&');

        if (split.Length == 0) return;

        foreach (var parameter in split)
        {
            // Two parts at most: a URL value may carry its own query string.
            var keyValueSplit = parameter.Split('=', 2);
            var key = HttpUtility.UrlDecode(keyValueSplit[0]).Trim();
            var value = (keyValueSplit.Length > 1 ? HttpUtility.UrlDecode(keyValueSplit[1]) : string.Empty).Trim();

            switch (key)
            {
                case "mode":
                    Instance.SetMode(value);
                    break;
                case "type":
                    Instance.SetType(value);
                    break;
                case "profile":
                    Instance.Profile = value;
                    break;
                case "emote":
                    Instance.Emote = value;
                    break;
                case "urn":
                    Instance.AddURN(value);
                    break;
                case "background":
                    Instance.SetBackground(value);
                    break;
                case "shadow":
                    Instance.SetShadow(value);
                    break;
                case "glow":
                    Instance.SetGlow(value);
                    break;
                case "skinColor":
                    Instance.SetSkinColor(value);
                    break;
                case "hairColor":
                    Instance.SetHairColor(value);
                    break;
                case "eyeColor":
                    Instance.SetEyeColor(value);
                    break;
                case "bodyShape":
                    Instance.BodyShape = value;
                    break;
                case "showAnimationReference":
                    Instance.ShowAnimationReference = bool.Parse(value);
                    break;
                case "projection":
                    Instance.Projection = value;
                    break;
                case "base64":
                    Instance.AddBase64(value);
                    break;
                case "contract":
                    Instance.Contract = value;
                    break;
                case "item":
                    Instance.ItemID = value;
                    break;
                case "token":
                    Instance.TokenID = value;
                    break;
                case "env":
                    APIService.Environment = value == "dev" ? "zone" : "org";
                    Debug.Log($"Using environment {APIService.Environment} (env value=[{value}])");
                    break;
                case "disableLoader":
                    Instance.DisableLoader = bool.Parse(value);
                    break;
                case "disableSwitcher":
                    Instance.DisableSwitcher = bool.Parse(value);
                    break;
                case "hideControls":
                    Instance.SetHideControls(value);
                    break;
                case "zoom":
                    Instance.SetZoomLevel(value);
                    break;
                case "wheelZoom":
                    Instance.SetWheelZoom(value);
                    break;
                case "wheelStart":
                    Instance.SetWheelStart(value);
                    break;
                case "camera":
                    Instance.SetCamera(value);
                    break;
                case "lockAlpha":
                    Instance.SetLockAlpha(value);
                    break;
                case "lockBeta":
                    Instance.SetLockBeta(value);
                    break;
                case "lockRadius":
                    Instance.SetLockRadius(value);
                    break;
                case "panning":
                    Instance.SetPanning(value);
                    break;
                case "disableAutoRotate":
                    Instance.SetDisableAutoRotate(value);
                    break;
                case "autoRotateSpeed":
                    Instance.SetAutoRotateSpeed(value);
                    break;
                case "offsetX":
                    Instance.SetOffsetX(value);
                    break;
                case "offsetY":
                    Instance.SetOffsetY(value);
                    break;
                case "showThumbnailBoundaries":
                    Instance.SetShowThumbnailBoundaries(value);
                    break;
                case "showSceneBoundaries":
                    Instance.SetShowSceneBoundaries(value);
                    break;
                case "offsetZ":
                    Instance.SetOffsetZ(value);
                    break;
                case "cameraX":
                    Instance.SetCameraX(value);
                    break;
                case "cameraY":
                    Instance.SetCameraY(value);
                    break;
                case "cameraZ":
                    Instance.SetCameraZ(value);
                    break;
                case "wheelPrecision":
                    Instance.SetWheelPrecision(value);
                    break;
                case "zoomScale":
                    Instance.SetZoomScale(value);
                    break;
                case "disableAutoCenter":
                    Instance.SetDisableAutoCenter(value);
                    break;
                case "socialEmote":
                    Instance.SetSocialEmote(value);
                    break;
                case "useBrowserPreload":
                    Instance.UseBrowserPreload = bool.Parse(value);
                    break;
                case "username":
                    Instance.Username = value;
                    break;
                case "showFPS":
                    Instance.ShowFPS = bool.Parse(value);
                    break;
                case "fps":
                    Instance.Fps = int.Parse(value);
                    break;
                case "concurrentLoad":
                    Instance.ConcurrentLoad = bool.Parse(value);
                    break;
                case "uninterruptedDeferAgent":
                    Instance.UninterruptedDeferAgent = bool.Parse(value);
                    break;
                case "disableFace":
                    Instance.SetDisableFace(value);
                    break;
                case "url":
                    Instance.AddUrl(value);
                    break;
                case "disableDefaultWearables":
                    Instance.SetDisableDefaultWearables(value);
                    break;
                case "disableDefaultEmotes":
                    Instance.SetDisableDefaultEmotes(value);
                    break;
                case "peerUrl":
                    Instance.SetPeerUrl(value);
                    break;
                case "marketplaceServerUrl":
                    Instance.SetMarketplaceServerUrl(value);
                    break;
                default:
                    Debug.LogWarning($"Unknown parameter in URL: {key}");
                    break;
            }
        }
    }

    /// <summary>
    /// The configuration as a query string <see cref="RecreateFrom"/> reads back: every value is
    /// encoded, so a URL with its own query, a base64 payload or a name with an ampersand survives.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        Append(sb, "mode", Mode.ToString().ToLowerInvariant());
        if (Type.HasValue) Append(sb, "type", Type.Value.ToString().ToLowerInvariant());
        Append(sb, "profile", Profile);
        Append(sb, "emote", _emote);
        Append(sb, "disableDefaultEmotes", DisableDefaultEmotes);
        foreach (var urn in Urns) Append(sb, "urn", urn);
        foreach (var url in Urls) Append(sb, "url", url);
        Append(sb, "disableDefaultWearables", DisableDefaultWearables);
        Append(sb, "disableFace", DisableFace);
        Append(sb, "peerUrl", PeerUrl);
        Append(sb, "marketplaceServerUrl", MarketplaceServerUrl);
        Append(sb, "background", ColorUtility.ToHtmlStringRGBA(Background));
        Append(sb, "shadow", Shadow ? "on" : "off");
        Append(sb, "glow", Glow ? "on" : "off");
        if (SkinColor.HasValue) Append(sb, "skinColor", ColorUtility.ToHtmlStringRGB(SkinColor.Value));
        if (HairColor.HasValue) Append(sb, "hairColor", ColorUtility.ToHtmlStringRGB(HairColor.Value));
        if (EyeColor.HasValue) Append(sb, "eyeColor", ColorUtility.ToHtmlStringRGB(EyeColor.Value));
        Append(sb, "bodyShape", BodyShape);
        Append(sb, "showAnimationReference", ShowAnimationReference);
        Append(sb, "projection", Projection);
        foreach (var bytes in Base64) Append(sb, "base64", Convert.ToBase64String(bytes));
        Append(sb, "contract", Contract);
        Append(sb, "item", ItemID);
        Append(sb, "token", TokenID);
        Append(sb, "env", APIService.Environment == "zone" ? "dev" : "prod");
        Append(sb, "disableLoader", DisableLoader);
        Append(sb, "disableSwitcher", DisableSwitcher);
        Append(sb, "hideControls", HideControls);
        if (ZoomLevel.HasValue) Append(sb, "zoom", ZoomLevel.Value);
        if (WheelZoom.HasValue) Append(sb, "wheelZoom", WheelZoom.Value);
        Append(sb, "wheelStart", WheelStart);
        Append(sb, "camera", Camera.ToString().ToLowerInvariant());
        Append(sb, "lockAlpha", LockAlpha);
        Append(sb, "lockBeta", LockBeta);
        Append(sb, "lockRadius", LockRadius);
        Append(sb, "panning", Panning);
        Append(sb, "disableAutoRotate", DisableAutoRotate);
        Append(sb, "autoRotateSpeed", AutoRotateSpeed);
        Append(sb, "offsetX", Offset.x);
        Append(sb, "offsetY", Offset.y);
        Append(sb, "showThumbnailBoundaries", ShowThumbnailBoundaries);
        Append(sb, "showSceneBoundaries", ShowSceneBoundaries);
        Append(sb, "offsetZ", OffsetDepth);
        if (CameraX.HasValue) Append(sb, "cameraX", CameraX.Value);
        if (CameraY.HasValue) Append(sb, "cameraY", CameraY.Value);
        if (CameraZ.HasValue) Append(sb, "cameraZ", CameraZ.Value);
        Append(sb, "wheelPrecision", WheelPrecision);
        Append(sb, "zoomScale", ZoomScale);
        Append(sb, "disableAutoCenter", DisableAutoCenter);
        if (SocialEmote != null) Append(sb, "socialEmote", SocialEmote.ToJson());
        Append(sb, "useBrowserPreload", UseBrowserPreload);
        Append(sb, "username", Username);
        Append(sb, "showFPS", ShowFPS);
        Append(sb, "fps", Fps);
        Append(sb, "concurrentLoad", ConcurrentLoad);
        Append(sb, "uninterruptedDeferAgent", UninterruptedDeferAgent);

        return sb.ToString();
    }

    // An unset or empty value is no parameter.
    private static void Append(StringBuilder sb, string key, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        sb.Append(sb.Length == 0 ? '?' : '&').Append(key).Append('=').Append(HttpUtility.UrlEncode(value));
    }

    private static void Append(StringBuilder sb, string key, bool value) => Append(sb, key, value.ToString());

    private static void Append(StringBuilder sb, string key, int value) =>
        Append(sb, key, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder sb, string key, float value) =>
        Append(sb, key, value.ToString(CultureInfo.InvariantCulture));
}

public enum PreviewMode
{
    Marketplace,
    Authentication,
    Profile,
    Builder,
    Configurator,
    Jesus,
}

/// <summary>
/// The views a preview can be opened in. Mirrors the wearable / avatar values of PreviewType in
/// @dcl/schemas, minus texture, which never reaches the renderer.
/// </summary>
public enum PreviewViewType
{
    Wearable,
    Avatar,
}

/// <summary>
/// Mirrors PreviewCamera in @dcl/schemas.
/// </summary>
public enum PreviewCameraKind
{
    Interactive,
    Static,
}