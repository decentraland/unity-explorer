using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.BudgetProvider;
using Unity.Profiling;
using UnityEngine;

[UpdateInGroup(typeof(PostRenderingSystemGroup))]
public partial class ResetFrameTimeBudgetProviderSystem : BaseUnityLoopSystem
{

    private readonly IConcurrentBudgetProvider frameTimeBudgetProvider;
    public int hiccupCounter;

    ProfilerRecorder mainThreadTimeRecorder;

    public ResetFrameTimeBudgetProviderSystem(World world, IConcurrentBudgetProvider frameTimeBudgetProvider) : base(world)
    {
        this.frameTimeBudgetProvider = frameTimeBudgetProvider;
        hiccupCounter = 0;
        //mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
    }

    protected override void Update(float t)
    {
        //frameTimeBudgetProvider.ReleaseBudget();
        //if (mainThreadTimeRecorder.CurrentValue > (35 * 1000000))
        //    hiccupCounter++;
    }
}
