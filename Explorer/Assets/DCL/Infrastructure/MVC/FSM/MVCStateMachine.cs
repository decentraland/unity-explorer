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
    public class MVCStateMachine<TBaseState> : IDisposable where TBaseState: class, IExitableState
    {
        public event Action<TBaseState>? OnStateChanged;

        private readonly Dictionary<Type, TBaseState> states = new ();
        private readonly CancellationTokenSource disposalCts = new ();

        private TBaseState? previousState;
        public TBaseState? CurrentState { get; private set; }

        public CancellationToken DisposalCt => disposalCts.Token;

        public MVCStateMachine<TBaseState> AddStates(params TBaseState[] states)
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

            return this;
        }

        public void Dispose()
        {
            disposalCts.SafeCancelAndDispose();
        }

        public MVCStateMachine<TBaseState> Enter<TState>() where TState: TBaseState, IState
        {
            TState state = ChangeState<TState>();
            state.Enter();
            OnStateChanged?.Invoke(state);
            return this;
        }

        public MVCStateMachine<TBaseState> Enter<TState, TPayload>(TPayload payload) where TState: TBaseState, IPayloadedState<TPayload>
        {
            TState state = ChangeState<TState>();
            state.Enter(payload);
            OnStateChanged?.Invoke(state);
            return this;
        }

        private TState ChangeState<TState>() where TState: TBaseState
        {
            Type newType = typeof(TState);

            // avoid changing to the same state
            if (CurrentState != null && CurrentState.GetType() == newType)
                return (TState)CurrentState;

            CurrentState?.Exit();

            if (!states.TryGetValue(newType, out TBaseState? newState))
            {
                var error = $"{GetType()}: state \"{newType}\" does not exist. Did you forget to add it while constructing state machine?";
                ReportHub.LogError(ReportCategory.MVC, error);
                throw new Exception(error);
            }

            previousState = CurrentState;
            CurrentState = newState;

            return (TState)newState;
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
