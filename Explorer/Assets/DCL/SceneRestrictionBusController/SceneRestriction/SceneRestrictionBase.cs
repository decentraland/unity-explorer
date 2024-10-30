namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public abstract class SceneRestrictionBase : ISceneRestriction
    {
        public SceneRestrictions Type { get; set; }
        public SceneRestrictionsAction Action { get; set; }
        public int EntityId { get; set; }
    }
}
