using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.BudgetProvider;

[UpdateInGroup(typeof(PostRenderingSystemGroup))]
public partial class ResetFrameTimeBudgetProviderSystem : BaseUnityLoopSystem
{

    private readonly IConcurrentBudgetProvider frameTimeBudgetProvider;

    public ResetFrameTimeBudgetProviderSystem(World world, IConcurrentBudgetProvider frameTimeBudgetProvider) : base(world)
    {
        this.frameTimeBudgetProvider = frameTimeBudgetProvider;
    }

    public override void AfterUpdate(in float t)
    {
        frameTimeBudgetProvider.ReleaseBudget();
    }

    protected override void Update(float t)
    {
    }
}
