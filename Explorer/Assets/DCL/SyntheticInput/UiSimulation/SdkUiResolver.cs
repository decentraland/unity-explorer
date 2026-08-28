using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Resolves SDK scene-UI elements by CRDT entity id in the current scene world. Names are useless for
    ///     addressing here — UI Toolkit element names are only built in the Editor — so the CRDT id a creator
    ///     sees in their scene code is the one stable key.
    /// </summary>
    public class SdkUiResolver
    {
        private static readonly QueryDescription UI_ELEMENTS = new QueryDescription().WithAll<UITransformComponent, CRDTEntity>();

        private readonly IScenesCache scenesCache;

        public SdkUiResolver(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public bool TryResolve(int crdtId, out SdkUiElement element, out string? failure)
        {
            element = default(SdkUiElement);

            if (!TryGetRunningSceneWorld(out World? maybeWorld, out failure))
                return false;

            World world = maybeWorld!;
            Entity found = Entity.Null;
            UITransformComponent? transform = null;

            // TODO (Vit): drop this scan by resolving through CrdtEcsSynchronizer.EntitiesMap (O(1)); done together
            // with the same scan in SyntheticPointerEventSystem/WorldInfo.
            world.Query(in UI_ELEMENTS, (Entity entity, ref UITransformComponent uiTransform, ref CRDTEntity crdtEntity) =>
            {
                if (crdtEntity.Id != crdtId)
                    return;

                found = entity;
                transform = uiTransform;
            });

            if (found == Entity.Null || transform == null)
            {
                failure = $"no UI entity with CRDT id {crdtId} in the current scene";
                return false;
            }

            world.TryGet(found, out UIInputComponent? input);
            world.TryGet(found, out UIDropdownComponent? dropdown);

            element = new SdkUiElement(world, found, transform!, input, dropdown, world.Has<PBPointerEvents>(found));
            return true;
        }

        /// <summary>
        ///     The scene-UI component owning a picked visual element: the entity whose UITransform element is the
        ///     element itself or its closest ancestor (a pick can land on an inner child, and UITransforms nest).
        ///     Null when nothing in the current scene owns the element.
        /// </summary>
        public UITransformComponent? ResolveComponent(VisualElement element)
        {
            if (!TryGetRunningSceneWorld(out World? maybeWorld, out _))
                return null;

            UITransformComponent? closest = null;
            var closestDistance = int.MaxValue;

            maybeWorld!.Query(in UI_ELEMENTS, (ref UITransformComponent uiTransform, ref CRDTEntity _) =>
            {
                var distance = 0;

                for (VisualElement? current = element; current != null; current = current.parent, distance++)
                {
                    if (!ReferenceEquals(uiTransform.Transform, current))
                        continue;

                    if (distance < closestDistance)
                    {
                        closest = uiTransform;
                        closestDistance = distance;
                    }

                    return;
                }
            });

            return closest;
        }

        /// <summary>
        ///     The panel the current scene's UI is attached to — the space positional gestures against scene UI are
        ///     expressed in. Any attached element identifies it: a scene renders its UI into one panel.
        /// </summary>
        public bool TryGetScenePanel(out IPanel? panel, out string? failure)
        {
            panel = null;

            if (!TryGetRunningSceneWorld(out World? maybeWorld, out failure))
                return false;

            World world = maybeWorld!;
            IPanel? found = null;

            world.Query(in UI_ELEMENTS, (ref UITransformComponent uiTransform, ref CRDTEntity _) =>
            {
                found ??= uiTransform.Transform.panel;
            });

            if (found == null)
            {
                failure = "the current scene has no UI attached to a panel";
                return false;
            }

            panel = found;
            return true;
        }

        /// <summary>Lists the interactable SDK-UI elements of the current scene (pointer targets, inputs, dropdowns, scrolls).</summary>
        public JArray ListInteractable()
        {
            var entries = new JArray();

            if (!TryGetRunningSceneWorld(out World? maybeWorld, out _))
                return entries;

            World world = maybeWorld!;

            world.Query(in UI_ELEMENTS, (Entity entity, ref UITransformComponent uiTransform, ref CRDTEntity crdtEntity) =>
            {
                if (uiTransform.IsHidden || uiTransform.Transform.panel == null)
                    return;

                bool hasInput = world.TryGet(entity, out UIInputComponent? _);
                bool hasDropdown = world.TryGet(entity, out UIDropdownComponent? _);
                bool hasScroll = uiTransform.InnerScrollView != null;
                bool hasPointerEvents = world.TryGet(entity, out PBPointerEvents? pointerEvents) && pointerEvents != null;

                if (!hasInput && !hasDropdown && !hasScroll && !hasPointerEvents)
                    return;

                var entry = new JObject
                {
                    ["stack"] = "sdk",
                    ["crdtId"] = crdtEntity.Id,
                    ["kind"] = hasInput ? "input" : hasDropdown ? "dropdown" : hasPointerEvents ? "pointerTarget" : "scroll",
                    ["screenRect"] = UiDiscovery.RectJson(UiScreenGeometry.PanelRectToImageRect(uiTransform.Transform.panel, uiTransform.Transform.worldBound)),
                };

                if (hasPointerEvents)
                {
                    var declaredEvents = new JArray();

                    foreach (PBPointerEvents.Types.Entry? pointerEvent in pointerEvents!.PointerEvents)
                        declaredEvents.Add(pointerEvent!.EventType.ToString());

                    entry["pointerEventTypes"] = declaredEvents;
                }

                entries.Add(entry);
            });

            return entries;
        }

        private bool TryGetRunningSceneWorld(out World? world, out string? failure)
        {
            world = null;
            failure = null;

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null || !scene.SceneStateProvider.IsCurrent || scene.SceneStateProvider.IsNotRunningState())
            {
                failure = "no running current scene";
                return false;
            }

            world = scene.EcsExecutor.World;
            return true;
        }
    }

    /// <summary>A resolved SDK scene-UI element: the entity's runtime UI components in its scene world.</summary>
    public readonly struct SdkUiElement
    {
        public readonly World World;
        public readonly Entity Entity;
        public readonly UITransformComponent Transform;
        public readonly UIInputComponent? Input;
        public readonly UIDropdownComponent? Dropdown;
        public readonly bool HasPointerEvents;

        public SdkUiElement(World world, Entity entity, UITransformComponent transform, UIInputComponent? input, UIDropdownComponent? dropdown, bool hasPointerEvents)
        {
            World = world;
            Entity = entity;
            Transform = transform;
            Input = input;
            Dropdown = dropdown;
            HasPointerEvents = hasPointerEvents;
        }
    }
}
