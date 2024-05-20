using System;

namespace SceneRuntime
{
    public interface IJsApiWrapper : IDisposable
    {
        void OnSceneIsCurrentChanged(bool isCurrent) { }

        void SetIsDisposing() { }
    }
}
