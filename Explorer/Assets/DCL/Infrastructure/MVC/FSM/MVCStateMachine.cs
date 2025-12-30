using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace MVC
{
    public interface IPayloadedState<in TPayload>
    {
        void Enter(TPayload payload);
    }

    public class MVCStateMachine<TBaseState, TContext> : IDisposable where TBaseState: MVCState<TBaseState, TContext>
    {
        public event Action? OnStateChanged;

        private readonly Dictionary<Type, TBaseState> states = new ();

        private readonly CancellationTokenSource disposalCts = new ();

        public MVCStateMachine(TContext context, params TBaseState[] states)
        {
            this.context = context;

            foreach (TBaseState state in states)
                AddState(state);
        }

        protected TContext context { get; }

        public float ElapsedTimeInState { get; private set; }
        public TBaseState? PreviousState { get; private set; }
        public TBaseState? CurrentState { get; private set; }

        /// <summary>
        ///     adds the state to the machine
        /// </summary>
        public void AddState(TBaseState state)
        {
            state.SetMachineAndContext(this, context, disposalCts.Token);
            states[state.GetType()] = state;
        }

        /// <summary>
        ///     ticks the state machine with the provided delta time
        /// </summary>
        public void Update(float deltaTime)
        {
            ElapsedTimeInState += deltaTime;
            CurrentState.Update(deltaTime);
        }

        /// <summary>
        ///     ticks the state machine with the provided delta time
        /// </summary>
        public void LateUpdate(float deltaTime)
        {
            ElapsedTimeInState += deltaTime;
            CurrentState.LateUpdate(deltaTime);
        }

        public void Enter<TState, TPayload>(TPayload payload, bool allowReEnterSameState = false) where TState: TBaseState, IPayloadedState<TPayload>
        {
            if (TryChangeCurrentState<TState>(allowReEnterSameState) && CurrentState is IPayloadedState<TPayload> payloadedState)
            {
                payloadedState.Enter(payload);
                ElapsedTimeInState = 0f;
                // fire the changed event if we have a listener
                OnStateChanged?.Invoke();
            }
        }

        /// <summary>
        ///     changes the current state
        /// </summary>
        public void Enter<TState>(bool allowReEnterSameState = false) where TState: TBaseState
        {
            if (TryChangeCurrentState<TState>(allowReEnterSameState))
            {
                CurrentState.Enter();
                ElapsedTimeInState = 0f;
                // fire the changed event if we have a listener
                OnStateChanged?.Invoke();
            }
        }

        /// <summary>
        ///     changes the current state
        /// </summary>
        private bool TryChangeCurrentState<TState>(bool allowReEnterSameState = false) where TState: TBaseState
        {
            // avoid changing to the same state
            Type newType = typeof(TState);

            if (CurrentState != null)
            {
                if (!allowReEnterSameState && CurrentState.GetType() == newType)
                    return false;

                CurrentState.Exit();
            }

            // do a sanity check while in the editor to ensure we have the given state in our state list
            if (!states.ContainsKey(newType))
            {
                var error = $"{GetType()}: state \"{newType}\" does not exist. Did you forget to add it by calling {nameof(AddState)}?";
                ReportHub.LogError(ReportCategory.MVC, error);
                throw new Exception(error);
            }

            // swap states and call begin
            PreviousState = CurrentState;
            CurrentState = states[newType];
            return true;
        }

        /// <summary>
        ///     Reverts to the single previous state, if one exists.
        /// </summary>
        public void PopState()
        {
            // Ensure there is a previous state to go back to.
            if (PreviousState == null)
            {
                return;
            }

            // End the current, swap to the previous.
            CurrentState.Exit();

            CurrentState = PreviousState;

            // After popping, there is no longer a "previous" state to go back to.
            // A new "previous" state will be set on the next call to ChangeState.
            PreviousState = null;

            // Begin the new (old) state.
            CurrentState.Enter();
            ElapsedTimeInState = 0f;

            OnStateChanged?.Invoke();
        }

        public void Dispose()
        {
            disposalCts.SafeCancelAndDispose();
        }
    }
}
