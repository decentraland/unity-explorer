using Arch.Core;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     Reads promises and reports their exceptions.
    ///     <para>It's very important to update this system right after the system that creates <see cref="StreamableLoadingResult{TAsset}" /> as promises are consumed in a closest update cycle</para>
    /// </summary>
    public abstract class ReportStreamableLoadingErrorSystem<TIntention, TAsset> : BaseUnityLoopSystem
        where TIntention: struct, IAssetIntention
    {
        private const LogType LOG_TYPE = LogType.Exception;

        private static readonly QueryDescription QUERY = new QueryDescription().WithAll<TIntention, StreamableLoadingResult<TAsset>>();

        private readonly IReportsHandlingSettings settings;

        private TryReport tryReport;

        protected ReportStreamableLoadingErrorSystem(World world, IReportsHandlingSettings settings) : base(world)
        {
            this.settings = settings;
            tryReport = new TryReport(GetReportData());
        }

        protected override void Update(float t)
        {
            // Skip the update entirely if the category is disabled
            if (!settings.CategoryIsEnabled(GetReportCategory(), LOG_TYPE))
                return;

            World.InlineQuery<TryReport, StreamableLoadingResult<TAsset>>(in QUERY, ref tryReport);
        }

        private readonly struct TryReport : IForEach<StreamableLoadingResult<TAsset>>
        {
            private readonly ReportData reportData;

            public TryReport(ReportData reportData)
            {
                this.reportData = reportData;
            }

            public void Update(ref StreamableLoadingResult<TAsset> streamableLoadingResult)
            {
                if (!streamableLoadingResult.Succeeded)
                    AssetsLoadingUtility.ReportException(reportData, streamableLoadingResult.Exception);
            }
        }
    }
}
