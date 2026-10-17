using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading;
using System;
using Unity.Profiling;

namespace DCL.Profiles
{
    public class ProfileData : StreamableRefCountData<Profile>
    {
        public ProfileData(Profile asset) : base(asset, ReportCategory.PROFILE) { }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.ProfilesAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.ProfilesReferenced;

        protected override void DestroyObject()
        {
            Asset.Dispose();
        }
    }
}
