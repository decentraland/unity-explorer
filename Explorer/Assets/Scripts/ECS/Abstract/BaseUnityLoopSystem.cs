﻿using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.Diagnostics;
using System;
using UnityEngine.Profiling;

namespace ECS.Abstract
{
    /// <summary>
    ///     Provides additional functionality to `BaseSystem`
    /// </summary>
    public abstract class BaseUnityLoopSystem : PlayerLoopSystem<World>
    {
        private readonly CustomSampler updateSampler;

        private string cachedCategory;

        protected BaseUnityLoopSystem(World world) : base(world)
        {
            updateSampler = CustomSampler.Create($"{GetType().Name}.Update");
        }

        public sealed override void Update(in float t)
        {
            try
            {
                updateSampler.Begin();
                Update(t);
                updateSampler.End();
            }
            catch (Exception e)
            {
                // enrich and propagate exception to the system group handler
                throw CreateException(e, ReportHint.None, true);
            }
        }

        protected abstract void Update(float t);

        protected internal string GetReportCategory()
        {
            // Look for category starting from the class itself and then groups recursively
            // if not found fall back to "ECS"

            if (cachedCategory != null)
                return cachedCategory;

            AttributesInfoBase metadata = GetMetadataInternal();
            LogCategoryAttribute logCategory = null;

            while (metadata != null && (logCategory = metadata.GetAttribute<LogCategoryAttribute>()) == null)
                metadata = metadata.GroupMetadata;

            return cachedCategory = logCategory?.Category ?? ReportCategory.ECS;
        }

        /// <summary>
        ///     Enriches exception with additional system-wise data
        /// </summary>
        protected EcsSystemException CreateException(Exception inner, ReportHint hint = ReportHint.None) =>
            CreateException(inner, hint, false);

        private EcsSystemException CreateException(Exception inner, ReportHint hint, bool unhandled) =>
            new (this, inner, new ReportData(GetReportCategory(), hint), unhandled);
    }
}
