using System;
using System.Buffers;

namespace Utility.Memory
{
    /// <summary>
    /// Doesn't own the memory, just a view to hack some APIs when lifetime is explicitly controlled, but Span is not acceptable.
    /// It's guaranteed that Js has one thread and the memory cannot be reassigned from multiple threads at the time.
    /// Supposed to be used for Js Wrappers.
    /// </summary>
    public class SingleUnmanagedMemoryManager<T> : MemoryManager<T> where T: unmanaged
    {
        private IntPtr ptr;
        private int length;

        public void Assign(IntPtr ptr, int length)
        {
            this.ptr = ptr;
            this.length = length;
        }

        public override Span<T> GetSpan()
        {
            unsafe 
            {
                return new ((void*) ptr, length);
            }
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            unsafe 
            {
                return new ((byte*)ptr + (elementIndex * sizeof(T)));
            }
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}
