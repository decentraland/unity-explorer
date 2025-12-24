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
        public event Action? OnStateChanged;

        private readonly Dictionary<Type, TBaseState> states = new ();

        private readonly CancellationTokenSource disposalCts = new ();

        private TBaseState? previousState;

        public TBaseState? CurrentState { get; private set; }

        public CancellationToken DisposalCt => disposalCts.Token;

        public MVCStateMachine() { }

        public MVCStateMachine(params TBaseState[] states)
        {
            AddStates(states);
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

        public void Enter<TState>() where TState: TBaseState, IState
        {
            TState state = ChangeState<TState>();
            state.Enter();
        }

        public void Enter<TState, TPayload>(TPayload payload) where TState: TBaseState, IPayloadedState<TPayload>
        {
            TState state = ChangeState<TState>();
            state.Enter(payload);
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
            OnStateChanged?.Invoke();

            return (TState)newState;
        }

        /// <summary>
        ///     Reverts to the previous state. Works only for IState (without payload).
        /// </summary>
        public void PopState()
        {
            if (previousState is not IState prevState)
                return;

            CurrentState?.Exit();
            CurrentState = previousState;
            previousState = null;

            prevState.Enter();
            OnStateChanged?.Invoke();
        }
    }
}
