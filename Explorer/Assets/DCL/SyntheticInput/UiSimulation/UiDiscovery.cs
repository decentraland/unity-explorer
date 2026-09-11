using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IPanel = UnityEngine.UIElements.IPanel;

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
        private readonly Dictionary<ulong, GameObject> lastListing = new ();
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

            Selectable[] selectables = UnityEngine.Object.FindObjectsByType<Selectable>();

            foreach (Selectable selectable in selectables)
            {
                if (!selectable.isActiveAndEnabled || !selectable.interactable)
                    continue;

                if (selectable.targetGraphic is { raycastTarget: false })
                    continue;

                Append(entries, selectable.gameObject, KindOf(selectable), checkOcclusion);
            }

            ScrollRect[] scrolls = UnityEngine.Object.FindObjectsByType<ScrollRect>();

            foreach (ScrollRect scroll in scrolls)
            {
                if (scroll.isActiveAndEnabled)
                    Append(entries, scroll.gameObject, "scroll", checkOcclusion);
            }

            return entries;
        }

        /// <summary>
        ///     What client UI, if anything, covers a screen point (Unity screen coordinates). A screen-addressed
        ///     world action must fail against the cover rather than aiming past it: a real click at that pixel
        ///     lands on the UI, never on the world behind it.
        ///     <para>
        ///         The raycast also carries UI Toolkit panels — a panel that picks an element at the point adds a
        ///         hit for its host GameObject — so <paramref name="hostedPanel" /> reports which panel a covering
        ///         hit belongs to. A caller that can name the picked element should describe the cover through the
        ///         panel instead of through <paramref name="path" />, which there names Unity plumbing.
        ///     </para>
        /// </summary>
        public bool TryFindCoverAt(Vector2 screenPoint, out string? path, out IPanel? hostedPanel)
        {
            pointerEventData.Reset();
            pointerEventData.position = screenPoint;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            if (raycastResults.Count == 0)
            {
                path = null;
                hostedPanel = null;
                return false;
            }

            RaycastResult topHit = raycastResults[0];
            UiOcclusion.TryGetHostedPanel(topHit, out hostedPanel);

            // A panel host reports the panel's selectableGameObject, which a panel need not have.
            GameObject? topObject = topHit.gameObject;
            path = topObject != null ? PathOf(topObject.transform) : "an unnamed UI surface";
            return true;
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
            global::AltTester.AltTesterUnitySDK.AltId[] altIds = UnityEngine.Object.FindObjectsByType<global::AltTester.AltTesterUnitySDK.AltId>();

            foreach (global::AltTester.AltTesterUnitySDK.AltId candidate in altIds)
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
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>();

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
            // EntityId.ToULong is the future-proof numeric form; the listing/addressing wire id carries it.
            ulong instanceId = EntityId.ToULong(element.GetEntityId());
            lastListing[instanceId] = element;

            Rect rect = UiScreenGeometry.ImageRectOf((RectTransform)element.transform);

            var entry = new JObject
            {
                ["stack"] = "ugui",
                ["id"] = instanceId,
                ["path"] = PathOf(element.transform),
                ["kind"] = kind,
                ["screenRect"] = RectJson(rect),
                ["center"] = CenterJson(rect),
            };

            string? label = LabelOf(element);

            if (label != null)
                entry["label"] = label;

#if ALTTESTER
            var altId = element.GetComponent<global::AltTester.AltTesterUnitySDK.AltId>();

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

            return text.Length <= LABEL_MAX_LENGTH ? text : text[..LABEL_MAX_LENGTH];
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

        /// <summary>
        ///     The screen size every reported rect is expressed in. Driver-facing output states it because a
        ///     screenshot may be downscaled: without it a rect cannot be turned into a normalized coordinate.
        /// </summary>
        internal static JObject ScreenJson() =>
            new ()
            {
                ["width"] = Screen.width,
                ["height"] = Screen.height,
            };

        /// <summary>The rect's center in the normalized form ui_drag takes (ui_click addresses elements by id, not by position).</summary>
        internal static JObject CenterJson(Rect rect)
        {
            Vector2 center = UiScreenGeometry.NormalizedCenterOf(rect);

            return new JObject
            {
                ["x"] = Math.Round(center.x, 4),
                ["y"] = Math.Round(center.y, 4),
            };
        }

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
