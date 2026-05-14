using System;

namespace DCL.UI
{
    /// <summary>
    ///     Stateful pairing for <see cref="NumericCyclerView"/>. Owns the current index, wraps on
    ///     overflow, drives the view's "current/total" text, and raises <see cref="OnIndexChanged"/>
    ///     only when the user steps via the arrows. External <see cref="SetIndex"/> calls update the
    ///     view silently so callers can drive the cycler from outside without re-entering.
    /// </summary>
    public class NumericCyclerController : IDisposable
    {
        public event Action<int>? OnIndexChanged;

        private readonly NumericCyclerView view;
        private readonly int total;
        private int currentIndex;

        public int CurrentIndex => currentIndex;

        public NumericCyclerController(NumericCyclerView view, int total, int initialIndex = 0)
        {
            this.view = view;
            this.total = total;
            currentIndex = Wrap(initialIndex);

            view.OnCycle += HandleCycle;
            UpdateView();
        }

        public void Dispose() =>
            view.OnCycle -= HandleCycle;

        public void SetIndex(int index)
        {
            currentIndex = Wrap(index);
            UpdateView();
        }

        private void HandleCycle(int delta)
        {
            currentIndex = Wrap(currentIndex + delta);
            UpdateView();
            OnIndexChanged?.Invoke(currentIndex);
        }

        private void UpdateView() =>
            view.SetIndex(currentIndex + 1, total);

        private int Wrap(int index) =>
            ((index % total) + total) % total;
    }
}