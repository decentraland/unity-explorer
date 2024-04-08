using System;

namespace SceneRuntime
{
    public interface IJsApiWrapper : IDisposable
    {
        void OnSceneBecameCurrent() { }
    }
}
