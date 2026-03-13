using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using DCL.FeatureFlags;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using Global.AppArgs;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.TestSuite
{
    public static class EcsTestsUtils
    {
        /// <summary>
        ///     Adds SDKTransform and creates a new GO with the entity Id as name
        /// </summary>
        public static TransformComponent AddTransformToEntity(World world, in Entity entity, bool isDirty = false)
        {
            var go = new GameObject($"{entity.Id}");
            Transform t = go.transform;

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var transformComponent = new TransformComponent(t);

            world.Add(entity, transformComponent, new SDKTransform { IsDirty = isDirty, Position = new CanBeDirty<Vector3>(Vector3.zero), Rotation = new CanBeDirty<Quaternion>(Quaternion.identity), Scale = Vector3.one });
            return transformComponent;
        }

        /// <summary>
        ///     Adds MaterialComponent and creates a new GO with the entity Id as name
        /// </summary>
        public static MaterialComponent AddMaterialToEntity(World world, in Entity entity)
        {
            var go = new GameObject($"{entity.Id}");
            Transform t = go.transform;

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var materialComponent = new MaterialComponent();
            materialComponent.Result = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));
            world.Add(entity, materialComponent);
            return materialComponent;
        }

        public static void SetUpFeaturesRegistry(params string[] flags)
        {
            var featureFlagsDto = new FeatureFlagsResultDto { flags = new Dictionary<string, bool>() };
            foreach (string flag in flags) featureFlagsDto.flags.Add(flag, true);
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(featureFlagsDto));

            FeaturesRegistry.Initialize(new FeaturesRegistry(new ApplicationParametersParser(), false));
        }

        public static void TearDownFeaturesRegistry()
        {
            FeatureFlagsConfiguration.Reset();
            FeaturesRegistry.Reset();
        }
    }
}
