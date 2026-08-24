using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Services;
using UnityEngine;
using Utils;

public class AangConfiguration
{
    public static AangConfiguration Instance { get; private set; } = new();

    private AangConfiguration()
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
    /// clap, dab, dance, fashion, fashion-2, fashion-3,fashion-4, love, money, fist-pump and head-explode
    /// </summary>
    public string Emote { get; set; } = "idle";

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
    public bool DisableFace { get; set; }

    public static void RecreateFrom(string url)
    {
        Instance = new AangConfiguration();

        if (string.IsNullOrEmpty(url) || !url.Contains('?')) return;

        var split = url[(url.IndexOf('?') + 1)..].Split('&');

        if (split.Length == 0) return;

        foreach (var parameter in split)
        {
            var keyValueSplit = parameter.Split('=');
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
                    Instance.DisableFace = bool.Parse(value);
                    break;
                default:
                    Debug.LogWarning($"Unknown parameter in URL: {key}");
                    break;
            }
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder("?");

        sb.AppendFormat("mode={0}", Mode.ToString().ToLowerInvariant());
        if (Type.HasValue)
            sb.AppendFormat("&type={0}", Type.Value.ToString().ToLowerInvariant());
        sb.AppendFormat("&profile={0}", Profile);
        sb.AppendFormat("&emote={0}", Emote);
        foreach (var urn in Urns)
            sb.AppendFormat("&urn={0}", urn);
        sb.AppendFormat("&background={0}", ColorUtility.ToHtmlStringRGBA(Background));
        if (SkinColor.HasValue)
            sb.AppendFormat("&skinColor={0}", ColorUtility.ToHtmlStringRGB(SkinColor.Value));
        if (HairColor.HasValue)
            sb.AppendFormat("&hairColor={0}", ColorUtility.ToHtmlStringRGB(HairColor.Value));
        if (EyeColor.HasValue)
            sb.AppendFormat("&eyeColor={0}", ColorUtility.ToHtmlStringRGB(EyeColor.Value));
        if (!string.IsNullOrEmpty(BodyShape))
            sb.AppendFormat("&bodyShape={0}", BodyShape);
        sb.AppendFormat("&showAnimationReference={0}", ShowAnimationReference);
        sb.AppendFormat("&projection={0}", Projection);
        foreach (var bytes in Base64)
            sb.AppendFormat("&base64={0}", Convert.ToBase64String(bytes));
        if (!string.IsNullOrEmpty(Contract))
            sb.AppendFormat("&contract={0}", Contract);
        if (!string.IsNullOrEmpty(ItemID))
            sb.AppendFormat("&item={0}", ItemID);
        if (!string.IsNullOrEmpty(TokenID))
            sb.AppendFormat("&token={0}", TokenID);
        sb.AppendFormat("&env={0}", APIService.Environment == "zone" ? "dev" : "prod");
        sb.AppendFormat("&disableLoader={0}", DisableLoader);
        sb.AppendFormat("&disableSwitcher={0}", DisableSwitcher);
        sb.AppendFormat("&useBrowserPreload={0}", UseBrowserPreload);
        sb.AppendFormat("&username={0}", Username);
        sb.AppendFormat("&showFPS={0}", ShowFPS);
        sb.AppendFormat("&fps={0}", Fps);
        sb.AppendFormat("&sequentialLoad={0}", ConcurrentLoad);
        sb.AppendFormat("&useUninterruptedDeferAgent={0}", UninterruptedDeferAgent);

        return sb.ToString();
    }
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