using System;

namespace DCL.Utilities
{
    public sealed class MappedReactiveProperty<TSource, TResult> : IReadonlyReactiveProperty<TResult>, IDisposable
    {
        private readonly IReadonlyReactiveProperty<TSource> source;
        private readonly Func<TSource, TResult> selector;

        public event Action<TResult>? OnUpdate;

        public TResult Value => selector(source.Value);

        public MappedReactiveProperty(IReadonlyReactiveProperty<TSource> source, Func<TSource, TResult> selector)
        {
            this.source = source;
            this.selector = selector;
            source.OnUpdate += OnSourceUpdated;
        }

        public void Dispose()
        {
            source.OnUpdate -= OnSourceUpdated;
        }

        private void OnSourceUpdated(TSource value)
        {
            OnUpdate?.Invoke(selector(value));
        }
    }

    public static class MappedReactivePropertyExtensions
    {
        public static MappedReactiveProperty<TSource, TResult> Select<TSource, TResult>(
            this IReadonlyReactiveProperty<TSource> source,
            Func<TSource, TResult> selector) =>
            new (source, selector);
    }
}
