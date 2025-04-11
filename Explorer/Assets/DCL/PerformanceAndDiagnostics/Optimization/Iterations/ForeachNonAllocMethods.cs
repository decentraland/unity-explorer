using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Optimization.Iterations
{
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public static class ForeachNonAllocMethods
    {
        /// <summary>
        /// ForeachNonAlloc.
        /// Use of the default <code>foreach(T item in list)</code> provokes allocation due of the boxing.
        /// This method is designed to avoid unnecessary allocations on the iterations.
        /// </summary>
        /// <param name="list">The list on which foreach executes.</param>
        /// <param name="action">Action to perform on the each element of the list.</param>
        /// <typeparam name="T">Type of the list elements.</typeparam>
        public static void ForeachNonAlloc<T>(this IReadOnlyList<T> list, Action<T> action)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                action(item);
            }
        }

        /// <summary>
        /// ForeachNonAlloc.
        /// Use of the default <code>foreach(T item in list)</code> provokes allocation due of the boxing.
        /// This method is designed to avoid unnecessary allocations on the iterations.
        /// To avoid boxing and closure capturing the context parameter is provided.
        /// </summary>
        /// <param name="list">The list on which foreach executes.</param>
        /// <param name="ctx">Context to provide for the action.</param>
        /// <param name="action">Action to perform on the each element of the list.</param>
        /// <typeparam name="T">Type of the list elements.</typeparam>
        /// <typeparam name="TCtx">Type of the provided context. It's useful to use value tuple to provide more than one value.</typeparam>
        public static void ForeachNonAlloc<T, TCtx>(this IReadOnlyList<T> list, TCtx ctx, Action<TCtx, T> action)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                action(ctx, item);
            }
        }
    }
}
