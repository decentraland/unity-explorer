using System;
using System.Collections.Generic;
using Data;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using Runtime.Wearables;
using Services;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;

namespace Loading
{
    public class WearableLoader : MonoBehaviour
    {
        [SerializeField] private Quaternion facialFeatureRotation = Quaternion.Euler(-15, 0, 0);

        // How far a solo upper-body item leans from its bind pose towards Idle. Not all the way: Idle
        // drops the arms against the ribs and folds a sleeve into the torso, hiding what is being sold,
        // while the bind pose splays them straight out. Upper body only - other categories have no
        // arm-clearance problem, or are read against the leg pose.
        [SerializeField, Range(0f, 1f)] private float upperBodyIdleBlend = 0.8f;

        private readonly List<Renderer> _outlineRenderers = new();

        private IDisposable _wearableDisposable;
        private GameObject _wearableGO;

        private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _defaultBodyFacialFeatures = new();

        public async Awaitable LoadWearable(EntityDefinition entityDefinition, BodyShape preferredBodyShape,
            AvatarColors colors, IReadOnlyDictionary<string, BonePose> idlePose)
        {
            Cleanup();

            var bodyShapeToLoad = entityDefinition.HasRepresentation(preferredBodyShape) ? preferredBodyShape :
                preferredBodyShape == BodyShape.Male ? BodyShape.Female : BodyShape.Male;

            switch (entityDefinition.Type)
            {
                case EntityType.Wearable:
                {
                    var loadResult = await GLTFLoader.LoadModel(bodyShapeToLoad, entityDefinition, transform);
                    _wearableDisposable = loadResult.Disposable;
                    _wearableGO = loadResult.Root;
                    _wearableGO.SetActive(true);
                    break;
                }
                case EntityType.FacialFeature:
                {
                    // It's a facial feature
                    var bodyEntity = EntityService.GetBodyEntity(bodyShapeToLoad);

                    // Load the body
                    var bodyLoadResult = await GLTFLoader.LoadModel(bodyShapeToLoad, bodyEntity, transform);
                    var ffLoadResult = await GLTFLoader.LoadFacialFeature(bodyShapeToLoad, entityDefinition);

                    _wearableDisposable = bodyLoadResult.Disposable;
                    _wearableGO = bodyLoadResult.Root;
                    _wearableGO.SetActive(true);

                    // Hide everything except the head
                    AvatarUtils.HideBodyShape(bodyLoadResult.Root, new HashSet<string>
                    {
                        WearableCategories.Categories.UPPER_BODY,
                        WearableCategories.Categories.LOWER_BODY,
                        WearableCategories.Categories.HANDS,
                        WearableCategories.Categories.FEET
                    }, new HashSet<string>());

                    AvatarUtils.HideBodyShapeFacialFeatures(bodyLoadResult.Root,
                        entityDefinition.Category != WearableCategories.Categories.EYES,
                        entityDefinition.Category != WearableCategories.Categories.EYEBROWS,
                        entityDefinition.Category != WearableCategories.Categories.MOUTH
                    );

                    AvatarUtils.SetupFacialFeatures(bodyLoadResult.Root, colors,
                        new Dictionary<string, LoadedFacialFeature>
                        {
                            [entityDefinition.URN] = ffLoadResult
                        }, _defaultBodyFacialFeatures);

                    bodyLoadResult.Root.transform.localRotation = facialFeatureRotation; // Tilt the head back        
                    break;
                }
                case EntityType.Body:
                case EntityType.Emote:
                default:
                    throw new NotSupportedException($"Trying to load unsupported wearable type: {entityDefinition.Type}");
            }

            _outlineRenderers.Clear();
            AvatarUtils.SetupWearable(_wearableGO, colors, _outlineRenderers);

            // Nothing animates this view, so posing the skeleton once is the whole job. It has to happen
            // before the frame CenterAndFit measures bounds from, or the item ends up off-centre.
            if (entityDefinition.Type == EntityType.Wearable
                && entityDefinition.Category == WearableCategories.Categories.UPPER_BODY)
                BlendTowardsPose(_wearableGO, idlePose, upperBodyIdleBlend);

            // Nothing to cast onto - the item floats with no floor, so a shadow only reaches the catcher
            // plane far below and reads as a smear. The plane is shared, so the item opts out instead.
            foreach (var wearableRenderer in _wearableGO.GetComponentsInChildren<Renderer>(true))
                wearableRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // The bones are still where the GLB put them, so the bind pose needs no separate copy. Matched
        // by name, and a bone the pose omits is left alone: not all wearables ship the canonical skeleton.
        private static void BlendTowardsPose(GameObject root, IReadOnlyDictionary<string, BonePose> pose,
            float weight)
        {
            if (weight <= 0f || pose.Count == 0) return;

            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
                if (pose.TryGetValue(bone.name, out var target))
                    target.BlendOnto(bone, weight);
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
            {
                RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.AddRange(_outlineRenderers);
            }
        }

        public void Cleanup()
        {
            Destroy(_wearableGO);
            _wearableDisposable?.Dispose();
            _wearableDisposable = null;

            _defaultBodyFacialFeatures.Clear();
        }
    }
}