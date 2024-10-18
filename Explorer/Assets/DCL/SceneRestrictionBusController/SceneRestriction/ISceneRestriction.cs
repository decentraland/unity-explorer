namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public interface ISceneRestriction
    {
        SceneRestrictions Type { get; set; }
        SceneRestrictionsAction Action { get; set; }
    }
}
