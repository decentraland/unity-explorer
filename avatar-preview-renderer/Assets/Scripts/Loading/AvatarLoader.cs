using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using JetBrains.Annotations;
using Rendering;
using Services;
using SpringBones;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Utils;

namespace Loading
{
    public class AvatarLoader : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        [FormerlySerializedAs("emoteEventReceiver")] [SerializeField]
        private EmoteAnimationController emoteAnimationController;

        [SerializeField] private Animation avatarAnimation;
        [SerializeField] private Transform avatarRootBone;
        [SerializeField] private Transform[] avatarBones;
        [SerializeField] private SpringBonesDriver springBonesDriver;

        [Header("Highlight"), SerializeField] private bool setsHighlight;
        [SerializeField] private Vector3 highlightCenter = new(0, 0.18f, 0);
        [SerializeField] private Vector2 highlightSize = new(0.57f, 2.3f);

        /// <summary>
        /// When true, the per-frame population of <see cref="RendererFeature_AvatarOutline"/> is
        /// skipped so the avatar renders without its outline. Set by the Outfit Studio (editor tool)
        /// for clean "card" beauty shots; the outline feature clears its list each frame, so simply
        /// not adding leaves it empty. Runtime-static (resets on domain reload); the studio re-applies
        /// it from its poller. Also honoured by <see cref="WearableLoader"/>.
        /// </summary>
        public static bool OutlineSuppressed;

        /// <summary>
        /// When true, the loaded emote's prop renderers join the avatar's in the outline pass.
        /// Off in production — props ship without a contour. Set by the Outfit Studio's DCL_Emotes
        /// mode, and only while its "Use Emote shader on props" knob is also on — that's when the
        /// prop is flattened to the same white as the avatar and would otherwise merge into it as
        /// one blank shape. Same runtime-static, studio-driven shape as
        /// <see cref="OutlineSuppressed"/>, which still wins over it.
        /// </summary>
        public static bool OutlineEmoteProps;

        private BodyShape? _loadedBodyShape;

        private readonly Dictionary<string, LoadedModel> _loadedModels = new();
        private readonly Dictionary<string, LoadedFacialFeature> _loadedFacialFeatures = new();
        private LoadedEmote? _loadedEmote;

        // Cached when the emote changes so the per-frame outline fill doesn't walk the prop
        // hierarchy. Only read while OutlineEmoteProps is on.
        private readonly List<Renderer> _emotePropRenderers = new();

        // JSBridge spring-bone overrides keyed by itemId; these take precedence over the
        // params declared in the wearable definition and are re-applied after every reload.
        private readonly Dictionary<string, Dictionary<string, SpringBoneParamsDTO>> _springBoneOverrides = new();

        private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _defaultBodyFacialFeatures = new();

        private readonly HashSet<string> _hiddenCategories = new();

        public async Awaitable LoadAvatar(BodyShape bodyShape, IEnumerable<EntityDefinition> wearableDefinitions,
            [CanBeNull] EntityDefinition emoteDefinition, string[] forceRenderCategories, AvatarColors colors)
        {
            var bodyEntity = EntityService.GetBodyEntity(bodyShape);
            var definitions = wearableDefinitions.Prepend(bodyEntity).ToList();

            var hiddenCategories = AvatarUtils.HideWearables(bodyShape, definitions, forceRenderCategories);

            var hasBodyShapeChanged = bodyShape != _loadedBodyShape;
            var definitionsToLoad = hasBodyShapeChanged
                ? definitions
                : definitions.Where(ed => !_loadedModels.ContainsKey(ed.URN) && !_loadedFacialFeatures.ContainsKey(ed.URN));

            var modelLoadTasks = new List<Task<LoadedModel>>();
            var facialFeaturesLoadTasks = new List<Task<LoadedFacialFeature>>();

            foreach (var def in definitionsToLoad)
            {
                if (def.Type is EntityType.Body or EntityType.Wearable)
                {
                    var task = GLTFLoader.LoadModel(bodyShape, def, transform);
                    modelLoadTasks.Add(task);
                    if (!PreviewConfiguration.Instance.ConcurrentLoad) await task;
                }
                else if (def.Type is EntityType.FacialFeature)
                {
                    var task = GLTFLoader.LoadFacialFeature(bodyShape, def);
                    facialFeaturesLoadTasks.Add(task);
                    if (!PreviewConfiguration.Instance.ConcurrentLoad) await task;
                }
                else
                {
                    throw new NotSupportedException("Trying to load entity type " + def.Type);
                }
            }

            var modelLoadResults = await Task.WhenAll(modelLoadTasks);
            var facialFeaturesLoadResults = await Task.WhenAll(facialFeaturesLoadTasks);
            var emoteLoadResult = emoteDefinition != null && emoteDefinition.URN != _loadedEmote?.Entity.URN
                ? await GLTFLoader.LoadEmote(bodyShape, emoteDefinition, transform)
                : (LoadedEmote?)null;

            var emoteChanged = _loadedEmote?.Entity.URN != emoteDefinition?.URN;

            // Clean up previous emote prop / audio
            if (emoteChanged && _loadedEmote != null)
            {
                _loadedEmote.Value.Disposable.Dispose();
                Destroy(_loadedEmote.Value.Prop);
            }

            if (emoteChanged)
            {
                _loadedEmote = emoteLoadResult;

                _emotePropRenderers.Clear();
                var prop = _loadedEmote?.Prop;
                if (prop != null) prop.GetComponentsInChildren(true, _emotePropRenderers);
            }

            var newModels = modelLoadResults.ToList();
            var newFacialFeatures = facialFeaturesLoadResults.ToList();

            // Remove already loaded models
            foreach (var urn in _loadedModels.Keys.ToList())
            {
                if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

                _loadedModels.Remove(urn, out var value);
                value.Disposable?.Dispose();
                Destroy(value.Root);
            }

            // Add new ones
            foreach (var loadedModel in newModels)
            {
                _loadedModels.Add(loadedModel.Entity.URN, loadedModel);
            }

            // Remove already loaded facial features
            foreach (var urn in _loadedFacialFeatures.Keys.ToList())
            {
                if (!hasBodyShapeChanged && definitions.Any(ed => ed.URN == urn)) continue;

                _loadedFacialFeatures.Remove(urn, out var value);

                Destroy(value.Main);
                Destroy(value.Mask);
            }

            // Add new ones
            foreach (var loadedFacialFeature in newFacialFeatures)
            {
                _loadedFacialFeatures.Add(loadedFacialFeature.Entity.URN, loadedFacialFeature);
            }

            // If body was changed we need to clear the default facial features
            if (hasBodyShapeChanged)
            {
                _defaultBodyFacialFeatures.Clear();
            }

            // Hide stuff on body shape if applicable and setup facial features
            var loadedCategories = _loadedModels.Values.Select(v => v.Entity.Category).ToHashSet();
            Assert.AreEqual(_loadedModels.Count, loadedCategories.Count, "We loaded a category twice");
            var bodyGO = _loadedModels.Values.FirstOrDefault(er => er.Entity.Type == EntityType.Body).Root;
            if (bodyGO != null)
            {
                AvatarUtils.HideBodyShape(bodyGO, hiddenCategories, loadedCategories);
                AvatarUtils.SetupFacialFeatures(bodyGO, colors, _loadedFacialFeatures, _defaultBodyFacialFeatures);
            }

            // Activate all models, setup colors, change root bone for animation
            RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.Clear();
            foreach (var (ed, go, _, outlineRenderers) in _loadedModels.Values)
            {
                go.SetActive(true);
                outlineRenderers.Clear();

                AvatarUtils.SetupWearable(go, colors, outlineRenderers, avatarRootBone, avatarBones);

                if (hiddenCategories.Contains(ed.Category))
                {
                    go.SetActive(false);
                }
            }

            // Spring bones: scan after SetupWearable so chain roots are already reparented
            // under live avatar bones (parent-driven animation propagation works automatically).
            // JSBridge overrides win over wearable definition params.
            if (springBonesDriver != null)
            {
                var liveBoneMap = new Dictionary<string, Transform>(avatarBones.Length);
                foreach (var b in avatarBones)
                    if (b != null) liveBoneMap[b.name] = b;
                springBonesDriver.AvatarBoneMap = liveBoneMap;

                springBonesDriver.UnregisterAll();

                // Prune overrides whose wearable is no longer loaded so the dict cannot grow unbounded
                foreach (var itemId in _springBoneOverrides.Keys.ToList())
                {
                    if (!TryFindWearableByItemId(itemId, out _))
                        _springBoneOverrides.Remove(itemId);
                }

                var ownersWithOverride = new HashSet<GameObject>();
                foreach (var (itemId, paramsByBone) in _springBoneOverrides)
                {
                    if (TryFindWearableByItemId(itemId, out var owner))
                    {
                        springBonesDriver.SetSpringChainsForWearable(owner, paramsByBone);
                        ownersWithOverride.Add(owner);
                    }
                }

                foreach (var loaded in _loadedModels.Values.Where(m => m.Root.activeSelf))
                {
                    if (ownersWithOverride.Contains(loaded.Root)) continue;
                    var meta = ConvertMetadataParams(loaded.Entity.GetSpringBoneParams(bodyShape));
                    if (meta != null && meta.Count > 0)
                        springBonesDriver.SetSpringChainsForWearable(loaded.Root, meta);
                }
            }
            else
            {
                Debug.LogError("[SpringBones] springBonesDriver not wired on AvatarLoader");
            }

            // If there is a new emote to be played
            if (emoteChanged)
            {
                if (_loadedEmote != null)
                {
                    emoteAnimationController.PlayEmote(_loadedEmote.Value);
                }
                else
                {
                    emoteAnimationController.StopEmote(true);
                }
            }

            _loadedBodyShape = bodyShape;

            // Update character bounds for background highlight
            UpdateHighlight();
        }

        public Camera MainCamera => mainCamera;

        /// <summary>
        /// Looks up a body-skeleton bone by its glTF node name (e.g. "Avatar_Head"). These
        /// transforms are the fixed base-body skeleton wearables get remapped onto by name in
        /// <see cref="AvatarUtils.SetupWearable"/>, so the same names apply here.
        /// </summary>
        [CanBeNull]
        public Transform GetBone(string name) => avatarBones?.FirstOrDefault(b => b != null && b.name == name);

        /// <summary>The base skeleton's head bone. Falls back to a name-suffix match in case the
        /// imported node name doesn't exactly match the "Avatar_Head" convention.</summary>
        [CanBeNull]
        public Transform HeadBone => GetBone("Avatar_Head") ??
            avatarBones?.FirstOrDefault(b => b != null && b.name.EndsWith("Head", StringComparison.OrdinalIgnoreCase));

        /// <summary>The base skeleton's neck bone (parent of <see cref="HeadBone"/>). Same
        /// exact-then-suffix fallback as <see cref="HeadBone"/>.</summary>
        [CanBeNull]
        public Transform NeckBone => GetBone("Avatar_Neck") ??
            avatarBones?.FirstOrDefault(b => b != null && b.name.EndsWith("Neck", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Stops the currently playing pose/emote clip so its bones stop being re-driven every
        /// frame, leaving them exactly where they are right now. Used by the Outfit Studio's
        /// "Look at Camera" action: without this, the legacy <see cref="Animation"/> component
        /// would re-sample the head/neck rotation from the clip on the very next frame and undo
        /// the look-at adjustment immediately.
        /// </summary>
        public void FreezePose() => avatarAnimation.Stop();

        public void SetSpringBonesParams(SpringBones.SpringBonesParamsPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.itemId)) return;
            if (springBonesDriver == null)
            {
                Debug.LogError("[SpringBones] springBonesDriver not wired on AvatarLoader");
                return;
            }

            // Cache so the override is re-applied after every reload (wins over wearable definition).
            _springBoneOverrides[payload.itemId] = payload.parameters;

            if (!TryFindWearableByItemId(payload.itemId, out var owner)) return;

            springBonesDriver.SetSpringChainsForWearable(owner, payload.parameters);
        }

        private static Dictionary<string, SpringBoneParamsDTO> ConvertMetadataParams(
            IReadOnlyDictionary<string, SpringBoneParamsDto> source)
        {
            if (source == null || source.Count == 0) return null;
            var result = new Dictionary<string, SpringBoneParamsDTO>(source.Count);
            foreach (var (boneName, m) in source)
            {
                result[boneName] = new SpringBoneParamsDTO
                {
                    stiffness = m.stiffness,
                    drag = m.drag,
                    gravityPower = m.gravityPower,
                    gravityDir = new[] { m.gravityDir.x, m.gravityDir.y, m.gravityDir.z },
                    isRoot = m.isRoot,
                };
            }
            return result;
        }

        private bool TryFindWearableByItemId(string itemId, out GameObject owner)
        {
            if (_loadedModels.TryGetValue(itemId, out var exact))
            {
                owner = exact.Root;
                return true;
            }
            foreach (var m in _loadedModels.Values)
            {
                if (m.Entity.URN != null && m.Entity.URN.EndsWith(itemId, StringComparison.Ordinal))
                {
                    owner = m.Root;
                    return true;
                }
            }
            foreach (var m in _loadedModels.Values)
            {
                if (m.Entity.URN != null && m.Entity.URN.Contains(itemId))
                {
                    owner = m.Root;
                    return true;
                }
            }
            owner = null;
            return false;
        }

        public void HideFacialFeatures()
        {
            var bodyGO = _loadedModels.Values.FirstOrDefault(er => er.Entity.Type == EntityType.Body).Root;
            if (bodyGO != null)
            {
                AvatarUtils.HideBodyShapeFacialFeatures(bodyGO, true, true, true);
            }
        }

        public void ClearEmote()
        {
            if (_loadedEmote != null)
            {
                _loadedEmote.Value.Disposable?.Dispose();
                Destroy(_loadedEmote.Value.Prop);
                _loadedEmote = null;
                _emotePropRenderers.Clear();
            }
        }

        public void TryHideCategory(string category, bool hidden)
        {
            var categoryGO = _loadedModels.Values.FirstOrDefault(c => c.Entity.Category == category).Root;
            categoryGO?.SetActive(!hidden);

            if (hidden)
            {
                _hiddenCategories.Add(category);
            }
            else
            {
                _hiddenCategories.Remove(category);
            }
        }

        /// <summary>
        /// Re-populates <see cref="RendererFeature_AvatarOutline"/>'s renderer list for a one-off
        /// manual camera render — specifically the Outfit Studio still capture, which issues an extra
        /// <c>SubmitRenderRequest</c>. <see cref="Update"/> fills the list every frame, but the outline
        /// pass clears it after each camera render (OnCameraCleanup), so a capture render that runs
        /// after the Game-view render in the same frame would otherwise draw no outline. Clears first
        /// so it's safe to call regardless of the list's current state.
        /// </summary>
        public void RefreshOutlineRenderers()
        {
            var list = RendererFeature_AvatarOutline.m_AvatarOutlineRenderers;
            list.Clear();
            if (OutlineSuppressed) return;

            foreach (var (_, root, _, outlineRenderers) in _loadedModels.Values)
            {
                if (root.activeInHierarchy)
                    list.AddRange(outlineRenderers);
            }

            AddEmotePropOutlines(list);
        }

        /// <summary>
        /// Appends the emote prop's renderers to an outline list, when the studio asked for it
        /// (<see cref="OutlineEmoteProps"/>). Appended last on purpose: the outline pass reads the
        /// FIRST entry's material to resolve the "Outline" pass index for the whole batch, and a
        /// wearable is the safer thing for it to read.
        ///
        /// Skips props whose material has no such pass. A prop is born on the scene shader and only
        /// becomes outline-capable once the studio's poll swaps it, so for a fraction of a second
        /// after every emote load it isn't — and drawing it at another shader's pass 0 would put a
        /// stray copy of the prop on screen, which a still captured in that window would keep.
        /// </summary>
        private void AddEmotePropOutlines(List<Renderer> list)
        {
            if (!OutlineEmoteProps) return;

            foreach (var renderer in _emotePropRenderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;

                var material = renderer.sharedMaterial;
                if (material == null || material.FindPass("Outline") < 0) continue;

                list.Add(renderer);
            }
        }

        private void Update()
        {
            if (OutlineSuppressed)
            {
                // Clear rather than just skip, in case the feature doesn't reset the list itself.
                RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.Clear();
            }
            else
            {
                foreach (var (_, root, _, outlineRenderers) in _loadedModels.Values)
                {
                    if (root.activeInHierarchy)
                    {
                        RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.AddRange(outlineRenderers);
                    }
                }

                AddEmotePropOutlines(RendererFeature_AvatarOutline.m_AvatarOutlineRenderers);
            }

            // Update character bounds every frame for dynamic positioning
            if (setsHighlight && _loadedModels.Count > 0)
            {
                UpdateHighlight();
            }
        }

        private void UpdateHighlight()
        {
            var worldCenter = transform.TransformPoint(highlightCenter);
            var worldSize = Vector2.Scale(highlightSize, transform.lossyScale);

            // TODO: Optimize, this can be done in 2 calls
            var leftSide = mainCamera.WorldToViewportPoint(worldCenter + mainCamera.transform.right * (worldSize.x / 2f));
            var rightSide = mainCamera.WorldToViewportPoint(worldCenter - mainCamera.transform.right * (worldSize.x / 2f));
            var topSide = mainCamera.WorldToViewportPoint(worldCenter + mainCamera.transform.up * (worldSize.y / 2f));
            var bottomSide = mainCamera.WorldToViewportPoint(worldCenter - mainCamera.transform.up * (worldSize.y / 2f));

            var vpCenter = mainCamera.WorldToViewportPoint(worldCenter);

            var viewportWidth = rightSide.x - leftSide.x;
            var viewportHeight = topSide.y - bottomSide.y;

            BackgroundRendererFeature.HighlightBounds = new Bounds(
                new Vector3(vpCenter.x, vpCenter.y),
                new Vector2(viewportWidth, viewportHeight));
        }
    }

    public readonly struct LoadedModel
    {
        public readonly EntityDefinition Entity;
        public readonly GameObject Root;
        public readonly IDisposable Disposable;
        public readonly List<Renderer> OutlineRenderers;

        public LoadedModel(EntityDefinition entity, GameObject root, IDisposable disposable)
        {
            Entity = entity;
            Root = root;
            Disposable = disposable;
            OutlineRenderers = new List<Renderer>();
        }

        public void Deconstruct(out EntityDefinition entity, out GameObject root, out IDisposable disposable,
            out List<Renderer> outlineRenderers)
        {
            entity = Entity;
            root = Root;
            disposable = Disposable;
            outlineRenderers = OutlineRenderers;
        }
    }

    public readonly struct LoadedFacialFeature
    {
        public readonly EntityDefinition Entity;
        public readonly Texture2D Main;
        public readonly Texture2D Mask;

        public LoadedFacialFeature(EntityDefinition entity, Texture2D main, Texture2D mask)
        {
            Entity = entity;
            Main = main;
            Mask = mask;
        }
    }

    public readonly struct LoadedEmote
    {
        public readonly EntityDefinition Entity;
        public readonly AnimationClip Clip;
        [CanBeNull] public readonly AudioClip Audio;
        [CanBeNull] public readonly GameObject Prop;
        [CanBeNull] public readonly Animation PropAnim;
        public readonly IDisposable Disposable;

        public LoadedEmote(EntityDefinition entity, AnimationClip clip, AudioClip audio, GameObject prop, Animation propAnim, IDisposable disposable)
        {
            Entity = entity;
            Clip = clip;
            Audio = audio;
            Prop = prop;
            PropAnim = propAnim;
            Disposable = disposable;
        }
    }
}