using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using NUnit.Framework;
using System;

namespace Diagnostics.ReportsHandling.Tests
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory("DirectCategorySystem")]
    public partial class DirectCategorySystem : BaseUnityLoopSystem
    {
        public DirectCategorySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            throw new NotImplementedException();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory("DirectCategoryGroup")]
    public partial class DirectCategoryGroup { }

    [UpdateInGroup(typeof(DirectCategoryGroup))]
    public partial class InheritedCategorySystem : BaseUnityLoopSystem
    {
        public InheritedCategorySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            throw new NotImplementedException();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FallbackCategorySystem : BaseUnityLoopSystem
    {
        public FallbackCategorySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            throw new NotImplementedException();
        }
    }

    [UpdateInGroup(typeof(DirectCategoryGroup))]
    [LogCategory("OverrideCategorySystem")]
    public partial class OverrideCategorySystem : BaseUnityLoopSystem
    {
        public OverrideCategorySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            throw new NotImplementedException();
        }
    }

    public class BaseUnityLoopSystemShould
    {

        public void DefineCategory()
        {
            var system = new DirectCategorySystem(null);
            Assert.That(system.GetReportCategory(), Is.EqualTo("DirectCategorySystem"));
        }


        public void InheritCategoryFromGroup()
        {
            var system = new InheritedCategorySystem(null);
            Assert.That(system.GetReportCategory(), Is.EqualTo("DirectCategoryGroup"));
        }


        public void OverrideCategoryFromGroup()
        {
            var system = new OverrideCategorySystem(null);
            Assert.That(system.GetReportCategory(), Is.EqualTo("OverrideCategorySystem"));
        }


        public void FallbackToEcsCategory()
        {
            var system = new FallbackCategorySystem(null);
            Assert.That(system.GetReportCategory(), Is.EqualTo(ReportCategory.ECS));
        }
    }
}
