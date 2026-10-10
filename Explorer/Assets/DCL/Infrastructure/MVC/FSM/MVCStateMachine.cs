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

        public TBaseState? PreviousState { get; private set; }
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

        public void Enter<TState>(bool allowReEnterSameState = false) where TState: TBaseState, IState
        {
            if (TryChangeState(out TState state, allowReEnterSameState))
            {
                state.Enter();
                OnStateChanged?.Invoke(state);
                ReportHub.Log(ReportCategory.MVC_STATE_MACHINE, $"{nameof(MVCStateMachine<TBaseState>)}<{typeof(TBaseState).Name}> Enter: {CurrentState?.GetType().Name}");
            }
        }

        public void Enter<TState, TPayload>(TPayload payload, bool allowReEnterSameState = false) where TState: TBaseState, IPayloadedState<TPayload>
        {
            if (TryChangeState(out TState state, allowReEnterSameState))
            {
                state.Enter(payload);
                OnStateChanged?.Invoke(state);
                ReportHub.Log(ReportCategory.MVC_STATE_MACHINE, $"{nameof(MVCStateMachine<TBaseState>)}<{typeof(TBaseState).Name}> Enter: {CurrentState?.GetType().Name} with payload: {payload.GetType().Name}");
            }
        }

        private bool TryChangeState<TState>(out TState state, bool allowReEnterSameState = false) where TState: TBaseState
        {
            Type newType = typeof(TState);
            ReportHub.Log(ReportCategory.MVC_STATE_MACHINE, $"{nameof(MVCStateMachine<TBaseState>)}<{typeof(TBaseState).Name}> trying to change states: {CurrentState?.GetType().Name} -> {newType.Name}");

            // Avoid changing to the same state
            if (!allowReEnterSameState && IsSameState())
            {
                state = (TState)CurrentState;
                ReportHub.Log(ReportCategory.MVC_STATE_MACHINE, $"{nameof(MVCStateMachine<TBaseState>)}<{typeof(TBaseState).Name}> is already in {CurrentState?.GetType().Name}");
                return false;
            }

            CurrentState?.Exit();
            ReportHub.Log(ReportCategory.MVC_STATE_MACHINE, $"{nameof(MVCStateMachine<TBaseState>)}<{typeof(TBaseState).Name}> Exit: <{CurrentState?.GetType().Name}>");

            if (!states.TryGetValue(newType, out TBaseState? newState))
            {
                var error = $"{GetType()}: state \"{newType}\" does not exist. Did you forget to add it while constructing state machine?";
                ReportHub.LogError(ReportCategory.MVC, error);
                throw new Exception(error);
            }

            PreviousState = CurrentState;
            CurrentState = newState;

            state = (TState)newState;
            return true;

            bool IsSameState() => CurrentState != null && CurrentState.GetType() == newType;
        }

        /// <summary>
        ///     Reverts to the previous state. Works only for IState (without payload).
        /// </summary>
        public bool TryPopState()
        {
            // Ensure there is a previous state to go back to and we can Enter it without payload (IState).
            if (PreviousState is not IState prevState)
            {
                ReportHub.LogWarning(ReportCategory.MVC, $"Can't pop the state. Previous state is not of type {nameof(IState)}!");
                return false;
            }

            CurrentState?.Exit();
            CurrentState = PreviousState;
            PreviousState = null;

            prevState.Enter();
            OnStateChanged?.Invoke(CurrentState);

            return true;
        }
    }
}
