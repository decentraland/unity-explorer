using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAttachmentsDTOList<T>
    {
        ConsumedList<T> ConsumeAttachments();

        static ConsumedList<T> DefaultConsumeAttachments(RepoolableList<T> value)
        {
            if (value.IsDisposed)
            {
                ReportHub.LogError(ReportCategory.AVATAR, $"Double consumption of {typeof(T).Name} occurred");
                return ConsumedList<T>.NewEmpty();
            }

            return new ConsumedList<T>(value);
        }
    }

    public readonly struct ConsumedList<T> : IDisposable
    {
        private readonly RepoolableList<T> value;

        public IReadOnlyList<T> Value => value.List;

        public ConsumedList(RepoolableList<T> value)
        {
            this.value = value;
        }

        public static ConsumedList<T> NewEmpty() =>
            new (RepoolableList<T>.NewList());

        public void Dispose()
        {
            value.Dispose();
        }
    }

    /// <summary>
    ///     Allows internal repooling in readonly structs.
    ///     It's created since ConsumedAttachmentsDTOList is needed in FinalizeEmoteAssetBundleSystem but StreamableLoadingResult is readonly.
    /// </summary>
    public class RepoolableList<T> : IDisposable
    {
        private static readonly ThreadSafeObjectPool<RepoolableList<T>> POOL = new (
            () => new RepoolableList<T>(new List<T>()),
            actionOnGet: l => l.isDisposed = false,
            actionOnRelease: l =>
            {
                l.isDisposed = true;
                l.list.Clear();
            }
        );

        private bool isDisposed;

        private readonly List<T> list;

        public bool IsDisposed => isDisposed;

        public List<T> List
        {
            get
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.AVATAR, $"Double consumption of {typeof(T).Name} occurred");
                    return new List<T>();
                }

                return list;
            }
        }

        private RepoolableList(List<T> list)
        {
            this.list = list;
        }

        public static RepoolableList<T> NewList() =>
            POOL.Get()!;

        public static RepoolableList<T> NewListWithContentOf(params T[] collection) =>
            NewListWithContentOf(collection as IEnumerable<T>);

        public static RepoolableList<T> NewListWithContentOf(IEnumerable<T> collection)
        {
            var repoolableList = NewList();
            repoolableList.List.AddRange(collection);
            return repoolableList;
        }

        public static RepoolableList<T> FromElement(T element)
        {
            var output = NewList();
            output.List.Add(element);
            return output;
        }

        public void Dispose()
        {
            POOL.Release(this);
        }
    }
}
