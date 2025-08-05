using RichTypes;
using System;

namespace Utility.Ownership
{
    /// <summary>
    /// Analogy with rust boxing concept to move values from stack to heap
    /// </summary>
    public class Box<T> : IBox<T> where T: struct
    {
        public Box(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }

    /// <summary>
    /// Referencing on a value belong the box itself, due of unsafe limitation and pointers Func is used
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LinkedBox<T> : IBox<T> where T: struct
    {
        private readonly Func<T> value;

        public LinkedBox(Func<T> value)
        {
            this.value = value;
        }

        public T Value => value();
    }

    public interface IBox<out T> where T: struct
    {
        T Value { get; }
    }

    public class Owned<T> where T: class
    {
        private T? resource;
        private bool disposed;

        public T Resource => disposed ? throw new ObjectDisposedException(typeof(T).Name) : resource!;

        public bool Disposed => disposed;

        public Owned(T resource)
        {
            this.resource = resource;
            disposed = false;
        }

        /// <summary>
        /// Caller is responsible to manually dispose the inner resource
        /// </summary>
        public void Dispose(out T? inner)
        {
            disposed = true;
            inner = resource;
            resource = null;
        }

        public Weak<T> Downgrade() =>
            new (this);
    }

    public readonly struct Weak<T> where T: class
    {
        public static Weak<T> Null;

        static Weak()
        {
            Owned<T> empty = new Owned<T>(null!);
            empty.Dispose(out _);
            Null = new Weak<T>(empty);
        }

        private readonly Owned<T> ownedResource;

        public Option<T> Resource => ownedResource.Disposed ? Option<T>.None : Option<T>.Some(ownedResource.Resource);

        internal Weak(Owned<T> ownedResource)
        {
            this.ownedResource = ownedResource;
        }
    }
}
