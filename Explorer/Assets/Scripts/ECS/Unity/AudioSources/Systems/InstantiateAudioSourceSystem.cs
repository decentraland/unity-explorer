using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.AudioSources.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.AudioSources.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class InstantiateAudioSourceSystem : BaseUnityLoopSystem
    {
        internal InstantiateAudioSourceSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            InstantiateAudioSourceQuery(World);
        }

        [Query]
        [All(typeof(PBAudioSource), typeof(TransformComponent))]
        [None(typeof(AudioSourceComponent))]
        private void InstantiateAudioSource(in Entity entity, ref PBAudioSource sdkAudioSource)
        {
            Debug.Log($"VV: {sdkAudioSource.AudioClipUrl} {sdkAudioSource}");
            // var component = new PrimitiveColliderComponent();
            // Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            // World.Add(entity, component);
        }
    }
}
