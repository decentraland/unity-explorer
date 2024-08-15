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
}
