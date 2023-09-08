using DCL.AvatarRendering.Wearables.Helpers;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearablesByPointersIntention
    {
        //TODO: Pool array
        public string[] Pointers;
        public WearablesLiterals.BodyShape BodyShape;
    }
}
