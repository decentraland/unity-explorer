using System;
using System.Collections.Generic;
using Data;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using Runtime.Wearables;
using Services;
using UnityEngine;
using Utils;

namespace Loading
{
    public class WearableLoader : MonoBehaviour
    {
        [SerializeField] private Quaternion facialFeatureRotation = Quaternion.Euler(-15, 0, 0);

        private readonly List<Renderer> _outlineRenderers = new();

        private IDisposable _wearableDisposable;
        private GameObject _wearableGO;

        private readonly Dictionary<string, (Texture2D main, Texture2D mask)> _defaultBodyFacialFeatures = new();

        public async Awaitable LoadWearable(EntityDefinition entityDefinition, BodyShape preferredBodyShape,
            AvatarColors colors)
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