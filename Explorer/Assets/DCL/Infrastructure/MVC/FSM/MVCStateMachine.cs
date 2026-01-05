using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace MVC
{
    /// <summary>
    ///     Generic state machine with typed CurrentState and support for payloaded states.
    /// </summary>
    /// <typeparam name="TBaseState">Base type for all states in this machine</typeparam>
    public class MVCStateMachine<TBaseState> : IDisposable where TBaseState: class, IState
    {
        public event Action<TBaseState>? OnStateChanged;

        private readonly Dictionary<Type, TBaseState> states = new ();
        private readonly CancellationTokenSource disposalCts = new ();

        private TBaseState? previousState;
        public TBaseState? CurrentState { get; private set; }

        public CancellationToken DisposalCt => disposalCts.Token;

        public void AddStates(params TBaseState[] states)
        {
            foreach (TBaseState state in states)
            {
                if (!this.states.TryAdd(state.GetType(), state))
                {
                    var error = $"Trying to add state that already exists in the machine - {state.GetType()}. Machine cannot have duplicate states.";
                    ReportHub.LogError(ReportCategory.MVC, error);
                    throw new Exception(error);
                }
            }
        }

        public void Dispose() =>
            disposalCts.SafeCancelAndDispose();

        public void Enter<TState>() where TState: TBaseState, IState
        {
            if (TryChangeState(out TState state))
            {
                state.Enter();
                OnStateChanged?.Invoke(state);
            }
        }

        private bool TryChangeState<TState>(out TState state) where TState: TBaseState
        {
            Type newType = typeof(TState);

            // Avoid changing to the same state
            if (CurrentState != null && CurrentState.GetType() == newType)
            {
                state = (TState)CurrentState;
                return false;
            }

            CurrentState?.Exit();

            if (!states.TryGetValue(newType, out TBaseState? newState))
            {
                var error = $"{GetType()}: state \"{newType}\" does not exist. Did you forget to add it while constructing state machine?";
                ReportHub.LogError(ReportCategory.MVC, error);
                throw new Exception(error);
            }

            previousState = CurrentState;
            CurrentState = newState;

            state = (TState)newState;
            return true;
        }

        /// <summary>
        ///     Reverts to the previous state. Works only for IState (without payload).
        /// </summary>
        public bool TryPopState()
        {
            // Ensure there is a previous state to go back to and we can Enter it without payload (IState).
            if (previousState is not IState prevState)
            {
                ReportHub.LogWarning(ReportCategory.MVC, $"Can't pop the state. Previous state is not of type {nameof(IState)}!");
                return false;
            }

            CurrentState?.Exit();
            CurrentState = previousState;
            previousState = null;

            prevState.Enter();
            OnStateChanged?.Invoke(CurrentState);

            return true;
        }
    }
}
