using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Groups;
using UnityEngine;

namespace ECS.Unity.AudioStreams.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]

    // [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class InstantiateAudioStreamSystem : BaseUnityLoopSystem
    {
        private InstantiateAudioStreamSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InstantiateAudioSource2Query(World);
        }

        [Query]
        private void InstantiateAudioSource2(ref PBAudioStream sdkAudioSource)
        {
            Debug.Log($"VV: 1 {sdkAudioSource.Url} {sdkAudioSource}");

            // var component = new PrimitiveColliderComponent();
            // Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            // World.Add(entity, component);
        }
    }
}
