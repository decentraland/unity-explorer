using UnityEngine;

namespace SceneRuntime.Apis.Modules.SignedFetch.Messages
{
    public struct SignedFetchRequest
    {
        public string url;
        public FlatFetchInit? init;

        public override readonly string ToString() =>
            JsonUtility.ToJson(this)!;
    }
}
