// using System;
// using System.Text;
// using UnityEngine;
// using System.Web;
// using Services;
//
// namespace Utils
// {
//     /// <summary>
//     /// Holds all the parameters passed in via the URL.
//     /// </summary>
//     public static class URLParser
//     {
//         public static AangConfiguration Parse(string url)
//         {
//             var config = new AangConfiguration();
//
//             if (string.IsNullOrEmpty(url) || !url.Contains('?')) return config;
//
//             var split = url[(url.IndexOf('?') + 1)..].Split('&');
//
//             if (split.Length == 0) return config;
//
//             foreach (var parameter in split)
//             {
//                 var keyValueSplit = parameter.Split('=');
//                 var key = HttpUtility.UrlDecode(keyValueSplit[0]);
//                 var value = keyValueSplit.Length > 1 ? HttpUtility.UrlDecode(keyValueSplit[1]) : string.Empty;
//
//                 switch (key)
//                 {
//                     case "mode":
//                         config.SetMode(value);
//                         break;
//                     case "profile":
//                         config.Profile = value;
//                         break;
//                     case "emote":
//                         config.Emote = value;
//                         break;
//                     case "urn":
//                         config.Urns.Add(value);
//                         break;
//                     case "background":
//                         config.SetBackground(value);
//                         break;
//                     case "skinColor":
//                         config.SetSkinColor(value);
//                         break;
//                     case "hairColor":
//                         config.SetHairColor(value);
//                         break;
//                     case "eyeColor":
//                         config.SetEyeColor(value);
//                         break;
//                     case "bodyShape":
//                         config.BodyShape = value;
//                         break;
//                     case "showAnimationReference":
//                         config.ShowAnimationReference = bool.Parse(value);
//                         break;
//                     case "projection":
//                         config.Projection = value;
//                         break;
//                     case "base64":
//                         config.AddBase64(value);
//                         break;
//                     case "contract":
//                         config.Contract = value;
//                         break;
//                     case "item":
//                         config.ItemID = value;
//                         break;
//                     case "token":
//                         config.TokenID = value;
//                         break;
//                     case "env":
//                         APIService.Environment = value == "dev" ? "zone" : "org";
//                         Debug.Log($"Using environment {APIService.Environment}");
//                         break;
//                     case "disableLoader":
//                         config.DisableLoader = bool.Parse(value);
//                         break;
//                     case "useBrowserPreload":
//                         config.UseBrowserPreload = bool.Parse(value);
//                         break;
//                     case "username":
//                         config.Username = value;
//                         break;
//                     case "showFPS":
//                         config.ShowFPS = bool.Parse(value);
//                         break;
//                     case "sequentialLoad":
//                         config.SequentialLoad = bool.Parse(value);
//                         break;
//                     case "uninterruptedDeferAgent":
//                         config.UninterruptedDeferAgent = bool.Parse(value);
//                         break;
//                     case "showEnterName":
//                         config.ShowEnterName = bool.Parse(value);
//                         break;
//                     default:
//                         Debug.LogWarning($"Unknown parameter in URL: {key}");
//                         break;
//                 }
//             }
//
//             return config;
//         }
//
//         /// <summary>
//         /// Converts the preview configuration into the matching set of url parameters.
//         /// Used for testing
//         /// </summary>
//         public static string GetUrlParameters(AangConfiguration config)
//         {
//             var sb = new StringBuilder("?");
//
//             sb.AppendFormat("mode={0}", config.Mode.ToString().ToLowerInvariant());
//             sb.AppendFormat("&profile={0}", config.Profile);
//             sb.AppendFormat("&emote={0}", config.Emote);
//             foreach (var urn in config.Urns)
//                 sb.AppendFormat("&urn={0}", urn);
//             sb.AppendFormat("&background={0}", ColorUtility.ToHtmlStringRGBA(config.Background));
//             if (config.SkinColor.HasValue)
//                 sb.AppendFormat("&skinColor={0}", ColorUtility.ToHtmlStringRGB(config.SkinColor.Value));
//             if (config.HairColor.HasValue)
//                 sb.AppendFormat("&hairColor={0}", ColorUtility.ToHtmlStringRGB(config.HairColor.Value));
//             if (config.EyeColor.HasValue)
//                 sb.AppendFormat("&eyeColor={0}", ColorUtility.ToHtmlStringRGB(config.EyeColor.Value));
//             if (!string.IsNullOrEmpty(config.BodyShape))
//                 sb.AppendFormat("&bodyShape={0}", config.BodyShape);
//             sb.AppendFormat("&showAnimationReference={0}", config.ShowAnimationReference);
//             sb.AppendFormat("&projection={0}", config.Projection);
//             foreach (var bytes in config.Base64)
//                 sb.AppendFormat("&base64={0}", Convert.ToBase64String(bytes));
//             if (!string.IsNullOrEmpty(config.Contract))
//                 sb.AppendFormat("&contract={0}", config.Contract);
//             if (!string.IsNullOrEmpty(config.ItemID))
//                 sb.AppendFormat("&item={0}", config.ItemID);
//             if (!string.IsNullOrEmpty(config.TokenID))
//                 sb.AppendFormat("&token={0}", config.TokenID);
//             sb.AppendFormat("&env={0}", APIService.Environment == "zone" ? "dev" : "prod");
//             sb.AppendFormat("&disableLoader={0}", config.DisableLoader);
//             sb.AppendFormat("&useBrowserPreload={0}", config.UseBrowserPreload);
//             sb.AppendFormat("&username={0}", config.Username);
//             sb.AppendFormat("&showFPS={0}", config.ShowFPS);
//             sb.AppendFormat("&sequentialLoad={0}", config.SequentialLoad);
//             sb.AppendFormat("&useUninterruptedDeferAgent={0}", config.UninterruptedDeferAgent);
//             sb.AppendFormat("&showEnterName={0}", config.ShowEnterName);
//
//             return sb.ToString();
//         }
//     }
// }