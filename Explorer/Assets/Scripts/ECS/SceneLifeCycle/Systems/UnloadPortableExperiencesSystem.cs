using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateBefore(typeof(UnloadSceneSystem))]
    public partial class UnloadPortableExperiencesSystem : BaseUnityLoopSystem
    {
        internal UnloadPortableExperiencesSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UnloadPortableExperienceRealmQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention), (typeof(PortableExperienceRealmComponent)))]
        private void UnloadPortableExperienceRealm(in Entity entity, ref PortableExperienceComponent portableExperienceComponent)
        {
            //We start another query from here using the data from the px component to match all other entities that were created by this PX
            UnloadLoadedPortableExperienceSceneQuery(World, portableExperienceComponent.Ens.ToString());
            World.Remove<PortableExperienceRealmComponent, PortableExperienceComponent>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UnloadLoadedPortableExperienceScene([Data] string sceneEntityId, in Entity entity, ref PortableExperienceComponent portableExperienceComponent)
        {
            //We only set to destroy those entities that have the same ens than the PX
            if (portableExperienceComponent.Ens.ToString() == sceneEntityId) World.Add<DeleteEntityIntention>(entity);
        }
    }
}
