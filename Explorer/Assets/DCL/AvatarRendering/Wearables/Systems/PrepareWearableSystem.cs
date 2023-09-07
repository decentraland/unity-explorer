namespace DCL.AvatarRendering.Wearables.Systems
{
    /*
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class PrepareWearableSystem : BaseUnityLoopSystem
    {
        private readonly string WEARABLE_CONTENT_BASE_URL;

        private SingleInstanceEntity wearableCatalog;

        public PrepareWearableSystem(World world, string wearableContentBaseURL) : base(world)
        {
            WEARABLE_CONTENT_BASE_URL = wearableContentBaseURL;
        }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            //TODO: How can we unify this two? Tried to do it with the interface with no luck
            CreateWearablesComponentFromResultPointerQuery(World);
            CreateWearablesComponentFromResultParamQuery(World);
        }

        [Query]
        private void CreateWearablesComponentFromResultPointer(in GetWearableByPointersIntention intention, ref StreamableLoadingResult<WearableDTO[]> wearableDTOResult)
        {
            // If the result failed, the result will be handled by the system that requested the wearables
            if (!wearableDTOResult.Succeeded)
                return;

            ProcessWearableRequestResult(intention, wearableDTOResult.Asset);
        }

        [Query]
        private void CreateWearablesComponentFromResultParam(in GetWearableByParamIntention intention, ref StreamableLoadingResult<WearableDTO[]> wearableDTOResult)
        {
            // If the result failed, the result will be handled by the system that requested the wearables
            if (!wearableDTOResult.Succeeded)
                return;

            ProcessWearableRequestResult(intention, wearableDTOResult.Asset);
        }

    }
    */
}
