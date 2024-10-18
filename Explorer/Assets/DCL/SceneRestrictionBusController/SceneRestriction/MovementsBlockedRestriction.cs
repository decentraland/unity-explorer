
namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class MovementsBlockedRestriction : SceneRestrictionBase
    {
        public bool DisableAll { get; set; }
        public bool DisableWalk { get; set; }
        public bool DisableJog { get; set; }
        public bool DisableRun { get; set; }
        public bool DisableJump { get; set; }
        public bool DisableEmote { get; set; }
    }
}
