using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.StreamableLoading.DeferredLoading.QualityReductors;

namespace ECS.StreamableLoading.DeferredLoading
{
    public class QualityReductorManager 
    {
        private readonly World World;

        private readonly List<QualityReductor> qualityReductors = new ();

        public QualityReductorManager(World world)
        {
            World = world;
            qualityReductors.Add(new AvatarQualityReductor());
            qualityReductors.Add(new LODQualityReductor());
        }

        public void RequestQualityReduction(World world)
        {
            foreach (var qualityReductor in qualityReductors)
                qualityReductor.RequestQualityReduction(world);
        }

        public void RequestQualityIncrease(World world)
        {
            foreach (var qualityReductor in qualityReductors)
                qualityReductor.RequestQualityIncrease(world);
        }
        
    }
}
