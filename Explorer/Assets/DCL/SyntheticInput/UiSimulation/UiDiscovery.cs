using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Enumerates and resolves interactable client-UI (uGUI) elements for automation drivers. Listing is a
    ///     cold path invoked per driver request; the instance-id registry it refreshes is what makes the
    ///     <c>id</c> address form valid until the next listing.
    /// </summary>
    public class UiDiscovery
    {
        private const int LABEL_MAX_LENGTH = 64;

        private readonly EventSystem eventSystem;
        private readonly PointerEventData pointerEventData;
        private readonly Dictionary<int, GameObject> lastListing = new ();
        private readonly List<RaycastResult> raycastResults = new ();
        private readonly List<Transform> candidateRoots = new ();
        private readonly List<string> pathSegments = new ();
        private readonly StringBuilder pathBuilder = new ();

        public UiDiscovery(EventSystem eventSystem)
        {
            this.eventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        /// <summary>Lists the interactable uGUI elements currently on screen, refreshing the instance-id registry.</summary>
        public JArray ListInteractable(bool checkOcclusion)
        {
            lastListing.Clear();
            var entries = new JArray();

            Selectable[] selectables = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);

            foreach (Selectable selectable in selectables)
            {
                if (!selectable.isActiveAndEnabled || !selectable.interactable)
                    continue;

                if (selectable.targetGraphic is { raycastTarget: false })
                    continue;

                Append(entries, selectable.gameObject, KindOf(selectable), checkOcclusion);
            }

            ScrollRect[] scrolls = UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);

            foreach (ScrollRect scroll in scrolls)
            {
                if (scroll.isActiveAndEnabled)
                    Append(entries, scroll.gameObject, "scroll", checkOcclusion);
            }

            return entries;
        }

        /// <summary>Resolves a uGUI address to a live GameObject, or explains why it cannot.</summary>
        public bool TryResolve(in UiElementAddress address, out GameObject? target, out string? failure)
        {
            target = null;
            failure = null;

            if (address.InstanceId is { } instanceId)
            {
                if (!lastListing.TryGetValue(instanceId, out GameObject? listed) || listed == null)
                {
                    failure = $"no element with id {instanceId} in the last ui_list result (the element is gone or the listing is stale — re-run ui_list)";
                    return false;
                }

                target = listed;
                return true;
            }

            if (address.AltId != null)
                return TryResolveAltId(address.AltId, out target, out failure);

            if (string.IsNullOrEmpty(address.Path))
            {
                failure = "a uGUI address needs a path, an id from ui_list, or an altId";
                return false;
            }

            return TryResolvePath(address.Path!, out target, out failure);
        }

        private bool TryResolveAltId(string altId, out GameObject? target, out string? failure)
        {
            target = null;
#if ALTTESTER
            AltTester.AltTesterUnitySDK.AltId[] altIds = UnityEngine.Object.FindObjectsByType<AltTester.AltTesterUnitySDK.AltId>(FindObjectsSortMode.None);

            foreach (AltTester.AltTesterUnitySDK.AltId candidate in altIds)
            {
                if (candidate.altID != altId)
                    continue;

                target = candidate.gameObject;
                failure = null;
                return true;
            }

            failure = $"no active element carries AltId '{altId}'";
            return false;
#else
            failure = "altId addressing requires an ALTTESTER build";
            return false;
#endif
        }

        private bool TryResolvePath(string path, out GameObject? target, out string? failure)
        {
            target = null;

            string[] segments = path.Split('/');

            if (segments.Length == 0)
            {
                failure = "empty path";
                return false;
            }

            Transform? current = FindRoot(segments[0]);

            if (current == null)
            {
                failure = $"no UI root named '{segments[0]}'";
                return false;
            }

            for (var i = 1; i < segments.Length; i++)
            {
                Transform? next = FindChild(current, segments[i]);

                if (next == null)
                {
                    failure = $"'{segments[i]}' not found under '{PathOf(current)}'";
                    return false;
                }

                current = next;
            }

            target = current.gameObject;
            failure = null;
            return true;
        }

        /// <summary>The element's addressable path: normalized names, with a "[n]" suffix for same-named siblings.</summary>
        public string PathOf(Transform transform)
        {
            pathSegments.Clear();

            for (Transform? current = transform; current != null; current = current.parent)
                pathSegments.Add(SegmentOf(current));

            pathBuilder.Clear();

            for (int i = pathSegments.Count - 1; i >= 0; i--)
            {
                pathBuilder.Append(pathSegments[i]);

                if (i > 0)
                    pathBuilder.Append('/');
            }

            return pathBuilder.ToString();
        }

        private static string SegmentOf(Transform transform)
        {
            ReadOnlySpan<char> name = UiElementAddress.NormalizeName(transform.name);
            Transform? parent = transform.parent;

            if (parent == null)
                return name.ToString();

            var ordinal = 0;

            for (var i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);

                if (sibling == transform)
                    break;

                if (UiElementAddress.NormalizeName(sibling.name).SequenceEqual(name))
                    ordinal++;
            }

            return ordinal == 0 ? name.ToString() : $"{name.ToString()}[{ordinal}]";
        }

        private Transform? FindRoot(string segment)
        {
            UiElementAddress.ParseSegment(segment, out ReadOnlySpan<char> name, out int siblingIndex);

            candidateRoots.Clear();
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            foreach (Canvas canvas in canvases)
            {
                Transform root = canvas.transform.root;

                if (!candidateRoots.Contains(root))
                    candidateRoots.Add(root);
            }

            var ordinal = 0;

            foreach (Transform root in candidateRoots)
            {
                if (!UiElementAddress.NormalizeName(root.name).SequenceEqual(name))
                    continue;

                if (ordinal == siblingIndex)
                    return root;

                ordinal++;
            }

            return null;
        }

        private static Transform? FindChild(Transform parent, string segment)
        {
            UiElementAddress.ParseSegment(segment, out ReadOnlySpan<char> name, out int siblingIndex);

            var ordinal = 0;

            for (var i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (!UiElementAddress.NormalizeName(child.name).SequenceEqual(name))
                    continue;

                if (ordinal == siblingIndex)
                    return child;

                ordinal++;
            }

            return null;
        }

        private void Append(JArray entries, GameObject element, string kind, bool checkOcclusion)
        {
            int instanceId = element.GetInstanceID();
            lastListing[instanceId] = element;

            Rect rect = UiScreenGeometry.ImageRectOf((RectTransform)element.transform);

            var entry = new JObject
            {
                ["stack"] = "ugui",
                ["id"] = instanceId,
                ["path"] = PathOf(element.transform),
                ["kind"] = kind,
                ["screenRect"] = RectJson(rect),
            };

            string? label = LabelOf(element);

            if (label != null)
                entry["label"] = label;

#if ALTTESTER
            var altId = element.GetComponent<AltTester.AltTesterUnitySDK.AltId>();

            if (altId != null)
                entry["altId"] = altId.altID;
#endif

            if (checkOcclusion)
            {
                pointerEventData.Reset();
                pointerEventData.position = UiScreenGeometry.ScreenCenterOf((RectTransform)element.transform);
                raycastResults.Clear();
                eventSystem.RaycastAll(pointerEventData, raycastResults);
                entry["occluded"] = !UiOcclusion.IsTopHitFor(element, raycastResults, out _);
            }

            entries.Add(entry);
        }

        private static string? LabelOf(GameObject element)
        {
            var tmpText = element.GetComponentInChildren<TMP_Text>();
            string? text = tmpText != null ? tmpText.text : element.GetComponentInChildren<Text>()?.text;

            if (string.IsNullOrEmpty(text))
                return null;

            return text!.Length <= LABEL_MAX_LENGTH ? text : text[..LABEL_MAX_LENGTH];
        }

        private static string KindOf(Selectable selectable) =>
            selectable switch
            {
                Button => "button",
                Toggle => "toggle",
                TMP_InputField or InputField => "inputField",
                Slider => "slider",
                TMP_Dropdown or Dropdown => "dropdown",
                Scrollbar => "scrollbar",
                _ => "selectable",
            };

        internal static JObject RectJson(Rect rect) =>
            new ()
            {
                ["x"] = Math.Round(rect.x, 1),
                ["y"] = Math.Round(rect.y, 1),
                ["width"] = Math.Round(rect.width, 1),
                ["height"] = Math.Round(rect.height, 1),
            };
    }
}
