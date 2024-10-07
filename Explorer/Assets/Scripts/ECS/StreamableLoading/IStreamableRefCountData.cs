using System;

namespace ECS.StreamableLoading
{
    public interface IStreamableRefCountData : IDisposable
    {
        void Dereference() { }

        public class Null : IStreamableRefCountData
        {
            public static readonly Null INSTANCE = new ();

            public void Dispose() { }
        }
    }
}
