using System;

namespace ECS.StreamableLoading
{
    public interface IAssetData : IDisposable
    {
        void Dereference() { }
    }
}
