using ECS.Prioritization;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Global.Dynamic
{
    public class DynamicWorldContainer
    {
        public IRealmController RealmController { get; private set; }

        public GlobalWorldFactory GlobalWorldFactory { get; private set; }

        public static DynamicWorldContainer Create(in StaticContainer staticContainer,
            IRealmPartitionSettings realmPartitionSettings,
            IReadOnlyList<int2> staticLoadPositions, int sceneLoadRadius) =>
            new ()
            {
                RealmController = new RealmController(sceneLoadRadius, staticLoadPositions),
                GlobalWorldFactory = new GlobalWorldFactory(in staticContainer, realmPartitionSettings, staticContainer.CameraSamplingData, new RealmSamplingData()),
            };
    }
}
