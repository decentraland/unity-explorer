using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace MVC
{
    public class MVCStateMachine<TBaseState> : IDisposable where TBaseState: MVCState<TBaseState>
    {
        public event Action? OnStateChanged;

        private readonly Dictionary<Type, TBaseState> states = new ();
        private TBaseState? previousState;
        public TBaseState? CurrentState { get; private set; }

        // controls state machine lifecycle
        private readonly CancellationTokenSource disposalCts = new ();
        public CancellationToken DisposalCt => disposalCts.Token;

        // Constructor with pre-defined states
        public MVCStateMachine(params TBaseState[] states)
        {
            foreach (TBaseState state in states)
                this.states[state.GetType()] = state;
        }

        public void AddStates(params TBaseState[] states)
        {
            foreach (TBaseState state in states)
                this.states[state.GetType()] = state;
        }

        public void Dispose()
        {
            disposalCts.SafeCancelAndDispose();
        }

        public void Enter<TState>() where TState: TBaseState
        {
            Type newType = typeof(TState);

            // avoid changing to the same state
            if (CurrentState != null)
            {
                if (CurrentState.GetType() == newType)
                    return;

                CurrentState.Exit();
            }

            // do a sanity check while in the editor to ensure we have the given state in our state list
            if (!states.ContainsKey(newType))
            {
                var error = $"{GetType()}: state \"{newType}\" does not exist. Did you forget to add it while constructing state machine?";
                ReportHub.LogError(ReportCategory.MVC, error);
                throw new Exception(error);
            }

            // swap states and call begin
            previousState = CurrentState;
            CurrentState = states[newType];
            CurrentState.Enter();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        ///     Reverts to the single previous state, if one exists.
        /// </summary>
        public void PopState()
        {
            // Ensure there is a previous state to go back to.
            if (previousState == null)
                return;

            // End the current, swap to the previous.
            CurrentState?.Exit();
            CurrentState = previousState;

            // After popping, there is no longer a "previous" state to go back to.
            // A new "previous" state will be set on the next call to ChangeState.
            previousState = null;

            // Begin the new (old) state.
            CurrentState.Enter();
            OnStateChanged?.Invoke();
        }
    }
}
