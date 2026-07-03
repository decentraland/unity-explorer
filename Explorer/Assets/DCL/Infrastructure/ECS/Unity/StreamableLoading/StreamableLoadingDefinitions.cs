using ECS.StreamableLoading.Common.Components;
using UnityEngine.Networking;

namespace ECS.StreamableLoading
{
    internal delegate UnityWebRequest CreateDelegate<TIntent>(in TIntent intention) where TIntent: struct, ILoadingIntention;
}
