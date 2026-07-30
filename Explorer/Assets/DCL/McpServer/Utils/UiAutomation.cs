#if MCP_TEST_AUTOMATION
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UITK = UnityEngine.UIElements;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Enumerates and drives the client's live UI by walking the running object hierarchy and clicking real
    ///     controls. Both UI systems the Explorer uses are walked: uGUI (the full Transform tree under every active
    ///     root <see cref="Canvas" />, so plain non-interactive nodes are addressable too — presence/absence waits are
    ///     the bulk of a UI suite) and UI-Toolkit (every element under each active <see cref="UITK.UIDocument" />'s
    ///     <c>rootVisualElement</c>). Each element gets a system-prefixed path (see <see cref="UiElementPath" />) that a
    ///     later call re-resolves against a fresh walk. Clicks and scrolls are hit-tested through the live
    ///     <see cref="EventSystem" />, so a control covered by a modal reports the blocker instead of a false success.
    ///     <para>
    ///         All members touch Unity UI objects and must run on the main thread; the MCP dispatcher already hops there
    ///         before a tool executes. The path rules live in the pure <see cref="UiElementPath" /> so they can be
    ///         unit-tested; the walk, the hit-test and the event dispatch are integration-only.
    ///     </para>
    /// </summary>
    public static class UiAutomation
    {
        private const string SYSTEM_UGUI = "ugui";
        private const string SYSTEM_UITK = "uitk";

        /// <summary>Nodes stored per walk. A live Explorer UI is well under this; the cap keeps a pathological tree bounded.</summary>
        private const int MAX_NODES = 6000;

        /// <summary>Elements serialized into one <c>list_ui_elements</c> answer; narrow past it with nameFilter.</summary>
        private const int MAX_ELEMENTS = 1500;

        /// <summary>Stands in for the value of a masked field, so a password or PIN never leaves the client.</summary>
        private const string MASKED_TEXT = "<masked>";

        // Main-thread-only scratch buffers: a walk happens inside one tool call and never overlaps another.
        private static readonly List<Handle> HANDLES = new (MAX_NODES);
        private static readonly Stack<(Transform node, string path)> PENDING = new ();
        private static readonly Stack<(UITK.VisualElement element, string path, string type)> PENDING_UITK = new ();
        private static readonly Dictionary<string, int> NAME_COUNTS = new ();
        private static readonly Dictionary<string, int> NAME_INDICES = new ();
        private static readonly List<RaycastResult> RAYCAST_RESULTS = new ();

        /// <summary>What <c>hover_ui</c> currently holds the pointer over, so the next hover can exit it first.</summary>
        private static GameObject? hovered;

        /// <summary>The answer every tool gives when a lookup matched nothing in the live hierarchy.</summary>
        public static McpToolResult NotFound(string element) =>
            McpToolResult.Error($"No UI element matched '{element}'. Call list_ui_elements to see current names/paths.");

        /// <summary>Lists every currently addressable UI element, optionally filtered by name or path substring.</summary>
        public static JArray Enumerate(string? filter, out bool truncated)
        {
            truncated = CollectAll();

            var array = new JArray();

            foreach (Handle handle in HANDLES)
            {
                if (!UiElementPath.Matches(handle.Name, handle.Path, filter))
                    continue;

                if (array.Count == MAX_ELEMENTS)
                {
                    truncated = true;
                    break;
                }

                array.Add(ToInfo(handle));
            }

            return array;
        }

        /// <summary>Resolves a path/name lookup to the single best-matching live element (highest match score wins).</summary>
        public static bool TryGetState(string query, out JObject state) =>
            TryResolve(query, out _, out state);

        /// <summary>
        ///     Resolves a lookup to the live <see cref="GameObject" /> behind a uGUI element, for the tools that read
        ///     components off it. UI-Toolkit elements are not GameObjects and resolve to false.
        /// </summary>
        public static bool TryResolveGameObject(string query, out GameObject gameObject, out string path)
        {
            if (TryResolve(query, out Handle handle) && handle.TryGetUgui(out Transform? node))
            {
                gameObject = node.gameObject;
                path = handle.Path;
                return true;
            }

            gameObject = null!;
            path = string.Empty;
            return false;
        }

        /// <summary>
        ///     Clicks the resolved element the way a user does: an <see cref="EventSystem" /> raycast at the element's
        ///     screen position decides what is actually on top, and the pointer down/up/click sequence goes to that hit.
        ///     A different element on top yields <c>clicked:false</c> plus the blocker instead of a false success.
        ///     UI-Toolkit controls receive a navigation-submit (what activating a focused Button/Toggle does).
        /// </summary>
        public static bool TryClick(string query, out JObject result)
        {
            if (!TryResolve(query, out Handle handle, out result))
                return false;

            if (handle.TryGetUitk(out UITK.VisualElement? element))
            {
                SubmitUitk(element);
                result["clicked"] = true;
                result["dispatch"] = "uitk-submit";
                return true;
            }

            // A handle pointing at neither UI system is the unmatched default, which answers like a failed lookup.
            if (!handle.TryGetUgui(out Transform? node))
                return false;

            HitTest hit = Raycast(node);
            Describe(result, hit);

            if (hit.Blocked)
            {
                result["clicked"] = false;
                return true;
            }

            GameObject target = hit.TryGetTopHit(out GameObject? topHit) ? topHit : node.gameObject;

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = hit.ScreenPoint,
                pressPosition = hit.ScreenPoint,
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = hit.Result,
                pointerPressRaycast = hit.Result,
                clickCount = 1,
            };

            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);

            result["clicked"] = true;
            return true;
        }

        /// <summary>
        ///     Moves the pointer onto the resolved element, hit-tested exactly like <see cref="TryClick" />: a control
        ///     covered by something else yields <c>hovered:false</c> plus the blocker rather than a hover state a user
        ///     could never produce. The previously hovered element receives its pointer-exit first, so a suite that
        ///     hovers one element after another leaves no stale highlight behind.
        /// </summary>
        public static bool TryHover(string query, out JObject result)
        {
            if (!TryResolve(query, out Handle handle, out result))
                return false;

            if (!handle.TryGetUgui(out Transform? node))
            {
                result["hovered"] = false;
                result["reason"] = "UI-Toolkit elements take their pointer events from their own panel; hover_ui drives uGUI only.";
                return true;
            }

            HitTest hit = Raycast(node);
            Describe(result, hit);

            if (hit.Blocked)
            {
                result["hovered"] = false;
                return true;
            }

            GameObject target = hit.TryGetTopHit(out GameObject? topHit) ? topHit : node.gameObject;

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = hit.ScreenPoint,
                pointerCurrentRaycast = hit.Result,
            };

            if (hovered != null && hovered != target)
                ExecuteEvents.ExecuteHierarchy(hovered, pointer, ExecuteEvents.pointerExitHandler);

            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerEnterHandler);
            hovered = target;

            result["hovered"] = true;
            return true;
        }

        /// <summary>
        ///     Writes <paramref name="text" /> into the resolved text field — uGUI <see cref="InputField" />,
        ///     <see cref="TMP_InputField" /> or a UI-Toolkit <see cref="UITK.TextField" /> — raising the same
        ///     value-changed notification an edit does, plus the end-edit/submit notification when
        ///     <paramref name="submit" /> is set (what pressing Enter in the field does).
        /// </summary>
        public static bool TrySetText(string query, string text, bool submit, out JObject result)
        {
            if (!TryResolve(query, out Handle handle, out result))
                return false;

            if (!handle.TryGetUgui(out Transform? node))
            {
                if (handle.Uitk is not UITK.TextField field)
                    return Reject(result, $"{handle.Type} is not a UI-Toolkit TextField.");

                // The value setter raises ChangeEvent<string> on the field, the notification a typed edit sends.
                field.value = text;

                if (submit)
                    SubmitUitk(field);
            }
            else
            {
                GameObject gameObject = node.gameObject;

                // Both text setters notify onValueChanged themselves; only the submit leg has to be raised here.
                if (gameObject.TryGetComponent(out TMP_InputField tmpField))
                {
                    tmpField.text = text;

                    if (submit)
                    {
                        tmpField.onEndEdit.Invoke(text);
                        tmpField.onSubmit.Invoke(text);
                    }
                }
                else if (gameObject.TryGetComponent(out InputField uguiField))
                {
                    uguiField.text = text;

                    if (submit)
                        uguiField.onEndEdit.Invoke(text);
                }
                else
                    return Reject(result, $"{handle.Type} is neither an InputField nor a TMP_InputField; only text fields accept set_ui_text.");
            }

            result["applied"] = true;
            result["submitted"] = submit;
            result["text"] = text;
            return true;
        }

        /// <summary>
        ///     Scrolls at the resolved element, hit-tested exactly like <see cref="TryClick" />: the wheel notification
        ///     goes to whatever handler actually sits under that screen position, so it reaches the enclosing
        ///     <c>ScrollRect</c> rather than a node that ignores it.
        /// </summary>
        public static bool TryScroll(string query, Vector2 delta, out JObject result)
        {
            if (!TryResolve(query, out Handle handle, out result))
                return false;

            if (!handle.TryGetUgui(out Transform? node))
            {
                result["scrolled"] = false;
                result["reason"] = "UI-Toolkit elements scroll through their own ScrollView; scroll drives uGUI only.";
                return true;
            }

            HitTest hit = Raycast(node);
            Describe(result, hit);

            if (!hit.TryGetTopHit(out GameObject? topHit))
            {
                // A wheel notification travels up from the raycast hit, so without one nothing is sent at all.
                result["dispatch"] = "none";
                result["scrolled"] = false;
                return true;
            }

            result["scrolled"] = !hit.Blocked && Dispatch(topHit, hit, delta);
            return true;
        }

        private static bool Reject(JObject result, string reason)
        {
            result["applied"] = false;
            result["reason"] = reason;
            return true;
        }

        private static bool Dispatch(GameObject target, in HitTest hit, Vector2 delta)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = hit.ScreenPoint,
                scrollDelta = delta,
                pointerCurrentRaycast = hit.Result,
            };

            return ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.scrollHandler) != null;
        }

        private static void SubmitUitk(UITK.VisualElement element)
        {
            using UITK.NavigationSubmitEvent evt = UITK.NavigationSubmitEvent.GetPooled();
            evt.target = element;
            element.SendEvent(evt);
        }

        private static bool TryResolve(string query, out Handle best)
        {
            CollectAll();

            best = default(Handle);
            var bestScore = 0;

            foreach (Handle handle in HANDLES)
            {
                int score = UiElementPath.MatchScore(handle.Path, handle.Name, query);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = handle;

                // The exact path list_ui_elements handed out is the highest score there is; nothing later can beat it.
                if (score == UiElementPath.SCORE_EXACT_PATH)
                    break;
            }

            return bestScore > 0;
        }

        /// <summary>Resolves <paramref name="query" /> and serializes the match in one step, for the tools that always need both.</summary>
        private static bool TryResolve(string query, out Handle handle, out JObject result)
        {
            if (!TryResolve(query, out handle))
            {
                result = null!;
                return false;
            }

            result = ToInfo(handle);
            return true;
        }

        /// <summary>
        ///     Rebuilds <see cref="HANDLES" /> from the live hierarchy: every active node under each root
        ///     <see cref="Canvas" />, then the UI-Toolkit trees. Both descents are iterative so one shared
        ///     name-index buffer serves every level. True when <see cref="MAX_NODES" /> ended a walk with part of
        ///     the hierarchy still unvisited, so the elements it left out are missing from the answer.
        /// </summary>
        private static bool CollectAll()
        {
            HANDLES.Clear();
            PENDING.Clear();

            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (canvas.isRootCanvas && canvas.gameObject.activeInHierarchy)
                    PENDING.Push((canvas.transform, UiElementPath.UGUI_PREFIX + AncestorPath(canvas.transform)));
            }

            while (PENDING.Count > 0 && HANDLES.Count < MAX_NODES)
            {
                (Transform node, string path) = PENDING.Pop();
                HANDLES.Add(CreateHandle(node, path));
                PushChildren(node, path);
            }

            bool truncated = PENDING.Count > 0;

            foreach (UITK.UIDocument document in Object.FindObjectsByType<UITK.UIDocument>(FindObjectsInactive.Exclude))
            {
                UITK.VisualElement? root = document.rootVisualElement;

                if (root == null)
                    continue;

                if (HANDLES.Count >= MAX_NODES)
                {
                    truncated = true;
                    break;
                }

                string name = string.IsNullOrEmpty(document.name) ? nameof(UITK.UIDocument) : document.name;
                truncated |= CollectUitk(root, UiElementPath.UITK_PREFIX + UiElementPath.Join(string.Empty, name));
            }

            return truncated;
        }

        /// <summary>
        ///     Queues the active children of <paramref name="node" /> with their paths, giving same-named siblings the
        ///     <c>Name[i]</c> indexer. Children go on in reverse so the stack pops them in Hierarchy order.
        /// </summary>
        private static void PushChildren(Transform node, string nodePath)
        {
            int childCount = node.childCount;

            if (childCount == 0)
                return;

            NAME_COUNTS.Clear();
            NAME_INDICES.Clear();

            for (var i = 0; i < childCount; i++)
            {
                Transform child = node.GetChild(i);

                if (!child.gameObject.activeSelf)
                    continue;

                // Object.name marshals a fresh string on every read, so each child's name is read once per pass.
                string name = child.name;
                NAME_COUNTS.TryGetValue(name, out int seen);
                NAME_COUNTS[name] = seen + 1;
            }

            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = node.GetChild(i);

                if (!child.gameObject.activeSelf)
                    continue;

                string name = child.name;

                // Counting down from the last same-named sibling, because the children are queued in reverse.
                NAME_INDICES.TryGetValue(name, out int taken);
                NAME_INDICES[name] = taken + 1;

                int total = NAME_COUNTS[name];
                string segment = UiElementPath.Segment(name, nameof(GameObject), total - taken - 1, total > 1);
                PENDING.Push((child, UiElementPath.Join(nodePath, segment)));
            }
        }

        /// <summary>
        ///     Adds a handle for every element below one <see cref="UITK.UIDocument" /> root, which itself carries
        ///     <paramref name="rootPath" />. True when <see cref="MAX_NODES" /> ended the walk with elements left.
        /// </summary>
        private static bool CollectUitk(UITK.VisualElement root, string rootPath)
        {
            PENDING_UITK.Clear();
            PushUitkChildren(root, rootPath);

            while (PENDING_UITK.Count > 0 && HANDLES.Count < MAX_NODES)
            {
                (UITK.VisualElement element, string path, string type) = PENDING_UITK.Pop();
                HANDLES.Add(new Handle(element, path, element.name ?? string.Empty, type));
                PushUitkChildren(element, path);
            }

            return PENDING_UITK.Count > 0;
        }

        /// <summary>
        ///     Queues the children of <paramref name="element" /> with their paths, giving same-named siblings the
        ///     <c>Name[i]</c> indexer so each one stays separately addressable. Children go on in reverse so the
        ///     stack pops them in document order.
        /// </summary>
        private static void PushUitkChildren(UITK.VisualElement element, string elementPath)
        {
            int childCount = element.hierarchy.childCount;

            if (childCount == 0)
                return;

            NAME_COUNTS.Clear();
            NAME_INDICES.Clear();

            for (var i = 0; i < childCount; i++)
            {
                string name = element.hierarchy.ElementAt(i).name ?? string.Empty;
                NAME_COUNTS.TryGetValue(name, out int seen);
                NAME_COUNTS[name] = seen + 1;
            }

            for (int i = childCount - 1; i >= 0; i--)
            {
                UITK.VisualElement child = element.hierarchy.ElementAt(i);
                string name = child.name ?? string.Empty;

                // Counting down from the last same-named sibling, because the children are queued in reverse.
                NAME_INDICES.TryGetValue(name, out int taken);
                NAME_INDICES[name] = taken + 1;

                int total = NAME_COUNTS[name];
                string type = child.GetType().Name;
                string segment = UiElementPath.Segment(name, type, total - taken - 1, total > 1);
                PENDING_UITK.Push((child, UiElementPath.Join(elementPath, segment), type));
            }
        }

        /// <summary>The hierarchy path of <paramref name="node" /> from its root, used to seed a walk.</summary>
        private static string AncestorPath(Transform node)
        {
            string path = UiElementPath.Join(string.Empty, node.name);

            for (Transform? current = node.parent; current != null; current = current.parent)
                path = UiElementPath.Join(string.Empty, current.name) + path;

            return path;
        }

        /// <summary>
        ///     The handle for one uGUI node, resolving the three components that answer for it — its control, its
        ///     label and its graphic — once, so serializing the node later looks none of them up again.
        /// </summary>
        private static Handle CreateHandle(Transform node, string path)
        {
            node.TryGetComponent(out Selectable? control);
            node.TryGetComponent(out TMP_Text? label);
            node.TryGetComponent(out Graphic? graphic);

            return new Handle(node, path, node.name, TypeOf(control, label, graphic), control, label, graphic);
        }

        /// <summary>
        ///     The most telling component on a node — the control if there is one, else the label or graphic that makes
        ///     it visible — so <c>type</c> stays useful now that plain nodes are enumerated too.
        /// </summary>
        private static string TypeOf(Selectable? control, TMP_Text? label, Graphic? graphic)
        {
            if (control != null) return control.GetType().Name;
            if (label != null) return label.GetType().Name;
            if (graphic != null) return graphic.GetType().Name;

            return nameof(GameObject);
        }

        private static JObject ToInfo(in Handle handle)
        {
            var info = new JObject
            {
                ["path"] = handle.Path,
                ["name"] = handle.Name,
                ["type"] = handle.Type,
            };

            string? text = null;

            if (handle.TryGetUgui(out Transform? node))
            {
                info["system"] = SYSTEM_UGUI;
                info["interactable"] = IsInteractable(handle);
                info["visible"] = IsVisible(node, handle.Graphic);
                text = UguiText(handle, node);
            }
            else if (handle.TryGetUitk(out UITK.VisualElement? element))
            {
                info["system"] = SYSTEM_UITK;
                info["interactable"] = element.enabledInHierarchy;
                info["visible"] = element.visible && element.resolvedStyle.display != UITK.DisplayStyle.None;
                text = UitkText(element);
            }

            if (text != null) info["text"] = text;

            return info;
        }

        /// <summary>Whether the node can take pointer input right now: an enabled control, or a raycastable graphic.</summary>
        private static bool IsInteractable(in Handle handle)
        {
            Selectable? control = handle.Control;

            if (control != null)
                return control.IsInteractable();

            Graphic? graphic = handle.Graphic;
            return graphic != null && graphic.raycastTarget && graphic.isActiveAndEnabled;
        }

        /// <summary>Whether the node is on screen: active in the hierarchy, with its graphic (if any) enabled.</summary>
        private static bool IsVisible(Transform node, Graphic? graphic) =>
            node.gameObject.activeInHierarchy && (graphic == null || graphic.enabled);

        /// <summary>
        ///     The node's user-visible text. Reads the control's own value first, then a label on the node, then the
        ///     nearest label beneath it (a Button's caption). TextMeshPro is checked before legacy uGUI text because
        ///     the Explorer's labels are overwhelmingly TMP. A field that masks its input on screen answers
        ///     <see cref="MASKED_TEXT" /> rather than the value the user typed.
        /// </summary>
        private static string? UguiText(in Handle handle, Transform node)
        {
            switch (handle.Control)
            {
                case TMP_InputField tmpField: return IsMasked(tmpField) ? MASKED_TEXT : tmpField.text;
                case InputField inputField: return IsMasked(inputField) ? MASKED_TEXT : inputField.text;
                case Toggle toggle: return toggle.isOn ? "on" : "off";
            }

            TMP_Text? label = handle.Label;
            if (label != null) return label.text;

            if (handle.Graphic is Text text) return text.text;

            TMP_Text? childTmpText = node.GetComponentInChildren<TMP_Text>();
            if (childTmpText != null) return childTmpText.text;

            Text? childText = node.GetComponentInChildren<Text>();
            return childText != null ? childText.text : null;
        }

        /// <summary>Whether the field hides what it holds behind asterisks, so its value is a secret.</summary>
        private static bool IsMasked(TMP_InputField field) =>
            field.contentType == TMP_InputField.ContentType.Password
            || field.contentType == TMP_InputField.ContentType.Pin
            || field.inputType == TMP_InputField.InputType.Password;

        /// <summary>Whether the field hides what it holds behind asterisks, so its value is a secret.</summary>
        private static bool IsMasked(InputField field) =>
            field.contentType == InputField.ContentType.Password
            || field.contentType == InputField.ContentType.Pin
            || field.inputType == InputField.InputType.Password;

        private static string? UitkText(UITK.VisualElement element)
        {
            switch (element)
            {
                case UITK.TextField textField: return textField.isPasswordField ? MASKED_TEXT : textField.value;
                case UITK.Toggle toggle: return toggle.value ? "on" : "off";
                case UITK.TextElement textElement: return textElement.text;
                default: return null;
            }
        }

        /// <summary>
        ///     Raycasts the live <see cref="EventSystem" /> at the centre of <paramref name="node" />, the way a mouse
        ///     click at that spot resolves. The hit is "blocked" when something that is neither the node nor one of its
        ///     descendants ends up on top — a modal over the control, which a direct dispatch would silently ignore.
        /// </summary>
        private static HitTest Raycast(Transform node)
        {
            if (!node.TryGetComponent(out RectTransform rect))
                return HitTest.Direct("the element has no RectTransform to aim at");

            Canvas? canvas = node.GetComponentInParent<Canvas>();

            if (canvas == null)
                return HitTest.Direct("the element is not under a Canvas");

            EventSystem? eventSystem = EventSystem.current;
            Camera? camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));

            if (eventSystem == null)
                return HitTest.Direct("no active EventSystem to hit-test with", screenPoint);

            RAYCAST_RESULTS.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPoint }, RAYCAST_RESULTS);

            if (RAYCAST_RESULTS.Count == 0)
                return HitTest.Direct("nothing raycastable at the element's centre", screenPoint);

            RaycastResult top = RAYCAST_RESULTS[0];

            // The graphic that answers is often a child (a Button's Image) or an ancestor drawing behind it; both count.
            return top.gameObject.transform.IsChildOf(node) || node.IsChildOf(top.gameObject.transform)
                ? new HitTest(top.gameObject, top, screenPoint, false, null)
                : new HitTest(top.gameObject, top, screenPoint, true,
                    $"'{top.gameObject.name}' covers '{node.name}' at that screen position; a real user could not interact with it.");
        }

        private static void Describe(JObject result, in HitTest hit)
        {
            result["screenX"] = Mathf.RoundToInt(hit.ScreenPoint.x);
            result["screenY"] = Mathf.RoundToInt(hit.ScreenPoint.y);

            if (hit.TryGetTopHit(out GameObject? topHit))
            {
                result["dispatch"] = hit.Blocked ? "blocked" : "raycast";
                result["topHit"] = topHit.name;
            }
            else
                result["dispatch"] = "direct";

            if (hit.Reason != null)
                result["reason"] = hit.Reason;
        }

        /// <summary>Where a click or scroll would actually land, and why, once the live EventSystem has answered.</summary>
        private readonly struct HitTest
        {
            public readonly GameObject? TopHit;
            public readonly RaycastResult Result;
            public readonly Vector2 ScreenPoint;

            /// <summary>Something else is on top, so the element itself never receives an interaction at this point.</summary>
            public readonly bool Blocked;

            /// <summary>Why the hit-test could not be used, or which element blocked it. Null on a clean hit.</summary>
            public readonly string? Reason;

            public HitTest(GameObject? topHit, RaycastResult result, Vector2 screenPoint, bool blocked, string? reason)
            {
                TopHit = topHit;
                Result = result;
                ScreenPoint = screenPoint;
                Blocked = blocked;
                Reason = reason;
            }

            /// <summary>The element the raycast landed on, when the hit-test produced a usable one.</summary>
            public bool TryGetTopHit([NotNullWhen(true)] out GameObject? topHit)
            {
                topHit = TopHit;
                return topHit != null;
            }

            /// <summary>A hit-test with no usable result, carrying the screen point it aimed at and why it failed.</summary>
            public static HitTest Direct(string reason, Vector2 screenPoint = default(Vector2)) =>
                new (null, default(RaycastResult), screenPoint, false, $"dispatched directly: {reason}");
        }

        /// <summary>
        ///     A resolved handle to one live UI element, either uGUI or UI-Toolkit, with its computed identity. A uGUI
        ///     handle also carries the components resolved during the walk, so serializing it looks none of them up again.
        /// </summary>
        private readonly struct Handle
        {
            public readonly Transform? Ugui;
            public readonly UITK.VisualElement? Uitk;

            /// <summary>The node's control, the label on it and the graphic it draws with; null when it has none.</summary>
            public readonly Selectable? Control;
            public readonly TMP_Text? Label;
            public readonly Graphic? Graphic;

            public readonly string Path;
            public readonly string Name;
            public readonly string Type;

            public Handle(Transform ugui, string path, string name, string type, Selectable? control, TMP_Text? label, Graphic? graphic)
            {
                Ugui = ugui;
                Uitk = null;
                Control = control;
                Label = label;
                Graphic = graphic;
                Path = path;
                Name = name;
                Type = type;
            }

            public Handle(UITK.VisualElement uitk, string path, string name, string type)
            {
                Ugui = null;
                Uitk = uitk;
                Control = null;
                Label = null;
                Graphic = null;
                Path = path;
                Name = name;
                Type = type;
            }

            /// <summary>The uGUI node this handle stands for, when it holds one rather than a UI-Toolkit element.</summary>
            public bool TryGetUgui([NotNullWhen(true)] out Transform? ugui)
            {
                ugui = Ugui;
                return ugui != null;
            }

            /// <summary>The UI-Toolkit element this handle stands for, when it holds one rather than a uGUI node.</summary>
            public bool TryGetUitk([NotNullWhen(true)] out UITK.VisualElement? uitk)
            {
                uitk = Uitk;
                return uitk != null;
            }
        }
    }
}
#endif
