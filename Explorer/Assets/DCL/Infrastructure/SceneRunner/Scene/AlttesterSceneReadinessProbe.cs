#if ALTTESTER
using System.Text;
using UnityEngine;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Static readiness probe queryable from AltTester via <c>AltDriver.CallStaticMethod</c>.
    ///     Tracks whichever <see cref="ISceneFacade"/> currently has <c>IsCurrent == true</c>
    ///     and exposes its loading state without going through the GameObject hierarchy.
    ///     Gated by the <c>ALTTESTER</c> compile define (stripped from release builds by
    ///     <c>CloudBuild.cs</c> when <c>IS_RELEASE_BUILD=true</c>), so the type is absent
    ///     from shipping binaries.
    /// </summary>
    public static class AlttesterSceneReadinessProbe
    {
        private static volatile ISceneFacade? currentFacade;

        internal static void SetCurrent(ISceneFacade facade)
        {
            currentFacade = facade;
        }

        internal static void ClearIfCurrent(ISceneFacade facade)
        {
            if (ReferenceEquals(currentFacade, facade))
                currentFacade = null;
        }

        /// <summary>
        ///     Returns <c>true</c> when there is a current scene AND its assets have finished loading
        ///     (i.e. <see cref="ISceneData.SceneLoadingConcluded"/> is set).
        /// </summary>
        public static bool IsCurrentSceneReady()
        {
            ISceneFacade? facade = currentFacade;
            return facade != null && facade.IsSceneReady();
        }

        /// <summary>
        ///     Returns the current scene's short name, or empty string if no scene is current.
        /// </summary>
        public static string GetCurrentSceneName()
        {
            ISceneFacade? facade = currentFacade;
            return facade?.Info.Name ?? string.Empty;
        }

        /// <summary>
        ///     Returns "x,y" for the current scene's base parcel, or empty string if no scene is current.
        /// </summary>
        public static string GetCurrentSceneBaseParcel()
        {
            ISceneFacade? facade = currentFacade;
            if (facade == null) return string.Empty;
            Vector2Int parcel = facade.Info.BaseParcel;
            var sb = new StringBuilder(16);
            sb.Append(parcel.x).Append(',').Append(parcel.y);
            return sb.ToString();
        }

        /// <summary>
        ///     Structured snapshot for AltTester. Shape:
        ///     <c>{"hasCurrent":true,"ready":false,"name":"...","baseParcel":"x,y","state":"Running"}</c>.
        ///     Hand-rolled JSON to avoid dragging a serializer dep into this assembly.
        /// </summary>
        public static string GetCurrentSceneStatusJson()
        {
            ISceneFacade? facade = currentFacade;

            var sb = new StringBuilder(128);
            sb.Append('{');

            if (facade == null)
            {
                sb.Append("\"hasCurrent\":false");
                sb.Append('}');
                return sb.ToString();
            }

            Vector2Int parcel = facade.Info.BaseParcel;
            SceneState state = facade.SceneStateProvider.State.Value();

            sb.Append("\"hasCurrent\":true,");
            sb.Append("\"ready\":").Append(facade.IsSceneReady() ? "true" : "false").Append(',');
            sb.Append("\"name\":\"").Append(EscapeJson(facade.Info.Name)).Append("\",");
            sb.Append("\"baseParcel\":\"").Append(parcel.x).Append(',').Append(parcel.y).Append("\",");
            sb.Append("\"state\":\"").Append(state).Append('"');
            sb.Append('}');
            return sb.ToString();
        }

        private static string EscapeJson(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            var sb = new StringBuilder(s!.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(c);      break;
                }
            }
            return sb.ToString();
        }
    }
}
#endif
