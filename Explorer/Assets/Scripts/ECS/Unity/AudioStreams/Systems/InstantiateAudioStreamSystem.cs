using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.AudioStreams.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]

    // [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class InstantiateAudioStreamSystem : BaseUnityLoopSystem
    {
        public InstantiateAudioStreamSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InstantiateAudioSourceQuery(World);
            InstantiateAudioSource2Query(World);
        }

        [Query]
        [All(typeof(PBAudioStream), typeof(TransformComponent))]
        private void InstantiateAudioSource(ref PBAudioStream sdkAudioSource)
        {
            Debug.Log($"VV: {sdkAudioSource.Url} {sdkAudioSource}");

            // var component = new PrimitiveColliderComponent();
            // Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            // World.Add(entity, component);
        }

        [Query]
        [All(typeof(PBAudioStream))]
        private void InstantiateAudioSource2(ref PBAudioStream sdkAudioSource)
        {
            Debug.Log($"VV: {sdkAudioSource.Url} {sdkAudioSource}");

            // var component = new PrimitiveColliderComponent();
            // Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            // World.Add(entity, component);
        }
    }
}
