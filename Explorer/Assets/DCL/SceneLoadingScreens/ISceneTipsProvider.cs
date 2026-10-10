namespace DCL.SceneLoadingScreens
{
    public interface ISceneTipsProvider
    {
        // TODO: in the future we may require the parcel coordinate to provide specific scene tips,
        // which would make this async again: UniTask<SceneTips> GetAsync(Vector2Int parcelCoord, CancellationToken ct)
        SceneTips Get();
    }
}
