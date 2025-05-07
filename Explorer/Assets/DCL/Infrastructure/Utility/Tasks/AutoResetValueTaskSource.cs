using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Utility.Tasks
{
    /// <summary>
    ///     see https://source.dot.net/#System.Net.Quic/System/Net/Quic/Internal/ResettableValueTaskSource.cs
    /// </summary>
    public class AutoResetValueTaskSource : IValueTaskSource
    {
        // None -> [TryGetValueTask] -> Awaiting -> [TrySetResult|TrySetException(final: false)] -> Ready -> [GetResult] -> None
        // None -> [TrySetResult|TrySetException(final: false)] -> Ready -> [TryGetValueTask] -> [GetResult] -> None
        // None|Awaiting -> [TrySetResult|TrySetException(final: true)] -> Completed(never leaves this state)
        // Ready -> [GetResult: TrySet*(final: true) was called] -> Completed(never leaves this state)
        private enum State
        {
            None,
            Awaiting,
            Ready,
            Completed,
        }

        private readonly TaskCompletionSource<bool> finalTaskSource;

        private ManualResetValueTaskSourceCore<bool> valueTaskSource;
        private State state;

        /// <summary>
        ///     Returns <c>true</c> is this task source has entered its final state, i.e. <see cref="TrySetResult(bool)" /> or <see cref="TrySetException(Exception, bool)" />
        ///     was called with <c>final</c> set to <c>true</c> and the result was propagated.
        /// </summary>
        public bool IsCompleted => (State)Volatile.Read(ref Unsafe.As<State, byte>(ref state)) == State.Completed;

        public AutoResetValueTaskSource()
        {
            valueTaskSource = new ManualResetValueTaskSourceCore<bool>();
            state = State.None;

            finalTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.None);
        }

        /// <summary>
        ///     Tries to transition from <see cref="State.Awaiting" /> to either <see cref="State.Ready" /> or <see cref="State.Completed" />, depending on the value of <paramref name="final" />.
        ///     Only the first call (with either value for <paramref name="final" />) is able to do that. I.e.: <c>TrySetResult()</c> followed by <c>TrySetResult(true)</c> will both return <c>true</c>.
        /// </summary>
        /// <param name="final">Whether this is the final transition to <see cref="State.Completed" /> or just a transition into <see cref="State.Ready" /> from which the task source can be reset back to <see cref="State.None" />.</param>
        /// <returns><c>true</c> if this is the first call that set the result; otherwise, <c>false</c>.</returns>
        public bool TrySetResult(bool final = false) =>
            TryComplete(null, final);

        /// <summary>
        ///     Tries to transition from <see cref="State.Awaiting" /> to either <see cref="State.Ready" /> or <see cref="State.Completed" />, depending on the value of <paramref name="final" />.
        ///     Only the first call is able to do that with the exception of <c>TrySetResult()</c> followed by <c>TrySetResult(true)</c>, which will both return <c>true</c>.
        /// </summary>
        /// <param name="final">Whether this is the final transition to <see cref="State.Completed" /> or just a transition into <see cref="State.Ready" /> from which the task source can be reset back to <see cref="State.None" />.</param>
        /// <param name="exception">The exception to set as a result of the value task.</param>
        /// <returns><c>true</c> if this is the first call that set the result; otherwise, <c>false</c>.</returns>
        public bool TrySetException(Exception exception, bool final = false) =>
            TryComplete(exception, final);

        /// <summary>
        ///     Tries to get a value task representing this task source. If this task source is <see cref="State.None" />, it'll also transition it into <see cref="State.Awaiting" /> state.
        ///     It prevents concurrent operations from being invoked since it'll return <c>false</c> if the task source was already in <see cref="State.Awaiting" /> state.
        ///     In other states, it'll return a value task representing this task source without any other work. So to determine whether to invoke a P/Invoke operation or not,
        ///     the state of <paramref name="valueTask" /> must also be checked.
        /// </summary>
        /// <param name="valueTask">A value task representing the result. Only meaningful in case this method returns <c>true</c>. Might already be completed.</param>
        /// <returns><c>true</c> if this is not an overlapping call (task source transitioned or was already set); otherwise, <c>false</c>.</returns>
        public bool TryGetValueTask(out ValueTask valueTask)
        {
            lock (this)
            {
                State state = this.state;

                // None: prepare for the actual operation happening and transition to Awaiting.
                if (state == State.None)
                    this.state = State.Awaiting;

                // None, Completed, Final: return the current task.
                if (state is State.None or State.Ready or State.Completed)
                {
                    valueTask = new ValueTask(this, valueTaskSource.Version);
                    return true;
                }

                // Awaiting: forbidden concurrent call.
                valueTask = default(ValueTask);
                return false;
            }
        }

        void IValueTaskSource.GetResult(short token)
        {
            var successful = false;

            try
            {
                valueTaskSource.GetResult(token);
                successful = true;
            }
            finally
            {
                lock (this)
                {
                    State state = this.state;

                    if (state == State.Ready)
                    {
                        valueTaskSource.Reset();
                        this.state = State.None;

                        // Propagate the _finalTaskSource result into _valueTaskSource if completed.
                        if (finalTaskSource.Task.IsCompleted)
                        {
                            this.state = State.Completed;

                            if (finalTaskSource.Task.IsCompletedSuccessfully) valueTaskSource.SetResult(true);
                            else
                            {
                                // We know it's always going to be a single exception since we're the ones setting it.
                                valueTaskSource.SetException(finalTaskSource.Task.Exception?.InnerException!);
                            }

                            // In case the _valueTaskSource was successful, we want the potential error from _finalTaskSource to surface immediately.
                            // In other words, if _valueTaskSource was set with success while final exception arrived, this will throw that exception right away.
                            if (successful) valueTaskSource.GetResult(valueTaskSource.Version);
                        }
                    }
                }
            }
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) =>
            valueTaskSource.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            valueTaskSource.OnCompleted(continuation, state, token, flags);
        }

        private bool TryComplete(Exception exception, bool final)
        {
            lock (this)
            {
                State state = this.state;

                // Completed: nothing to do.
                if (state == State.Completed) return false;

                // If the _valueTaskSource has already been set, we don't want to lose the result by overwriting it.
                // So keep it as is and store the result in _finalTaskSource.
                if (state == State.None ||
                    state == State.Awaiting) this.state = final ? State.Completed : State.Ready;

                // Unblock the current task source and in case of a final also the final task source.
                if (exception is not null)
                {
                    if (state is State.None or State.Awaiting)
                        valueTaskSource.SetException(exception);

                    if (final)
                        return finalTaskSource.TrySetException(exception);

                    return state != State.Ready;
                }

                if (state is State.None or State.Awaiting) valueTaskSource.SetResult(final);

                if (final)
                    return finalTaskSource.TrySetResult(true);

                return state != State.Ready;
            }
        }
    }
}
