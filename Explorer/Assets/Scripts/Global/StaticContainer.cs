using Arch.SystemGroups;
using Diagnostics;
using Diagnostics.ReportsHandling;
using ECS.BudgetProvider;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using System;

namespace Global
{
    /// <summary>
    ///     Produces dependencies that never change during the lifetime of the application
    ///     and are not connected to the global world or scenes but are used by them.
    ///     This is the first container to instantiate, should not depend on any other container
    /// </summary>
    public class StaticContainer : IDisposable
    {
        public DiagnosticsContainer DiagnosticsContainer { get; private set; }

        public ComponentsContainer ComponentsContainer { get; private set; }

        public ISystemGroupAggregate<IPartitionComponent>.IFactory WorldsAggregateFactory { get; private set; }

        public IPartitionSettings PartitionSettings { get; private set; }

        public CameraSamplingData CameraSamplingData { get; private set; }

        public IReportsHandlingSettings ReportsHandlingSettings { get; private set; }

        public IConcurrentBudgetProvider InstantiationBudgetProvider { get; private set; }


        public void Dispose()
        {
            DiagnosticsContainer?.Dispose();
        }

        public static StaticContainer Create(IPartitionSettings partitionSettings, IReportsHandlingSettings reportsHandlingSettings) =>
            new ()
            {
                DiagnosticsContainer = DiagnosticsContainer.Create(reportsHandlingSettings),
                ComponentsContainer = ComponentsContainer.Create(),
                PartitionSettings = partitionSettings,
                WorldsAggregateFactory = new PartitionedWorldsAggregate.Factory(),
                CameraSamplingData = new CameraSamplingData(),
                ReportsHandlingSettings = reportsHandlingSettings,
                InstantiationBudgetProvider = new FrameTimeBudgetProvider(1f, new FrameTimeCounter())
            };
    }
}
