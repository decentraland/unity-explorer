using Cysharp.Threading.Tasks;

namespace DCL.ScenesDebug.ScenesConsistency.Conditions
{
    enum NextSceneDelayType
    {
        ByTime,
        BySubmit,
    }

    public interface INextSceneDelay
    {
        UniTask WaitAsync();
    }
}
