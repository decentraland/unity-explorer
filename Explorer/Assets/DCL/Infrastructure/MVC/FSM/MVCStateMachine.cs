using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace MVC
{
    public class MVCStateMachine<TBaseState, TContext> where TBaseState: MVCState<TBaseState, TContext>
    {
        public event Action? OnStateChanged;

        private readonly Dictionary<Type, TBaseState> states = new ();

        public MVCStateMachine(TContext context, TBaseState initialState)
        {
            this.context = context;

            // setup our initial state
            AddState(initialState);
            CurrentState = initialState;
            CurrentState.Begin();
        }

        protected TContext context { get; }

        public float ElapsedTimeInState { get; private set; }
        public TBaseState? PreviousState { get; private set; }
        public TBaseState CurrentState { get; private set; }

        /// <summary>
        ///     adds the state to the machine
        /// </summary>
        public void AddState(TBaseState state)
        {
            state.SetMachineAndContext(this, context);
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

        public R ChangeState<R>(R state) where R: TBaseState
        {
            // Make sure that this state is registered in the state machine
            if (!states.TryGetValue(typeof(R), out TBaseState? registeredState) || registeredState != state)
            {
                var error = $"{GetType()}: state \"{typeof(R)}\" {state} is not registered";
                ReportHub.LogError(ReportCategory.MVC, error);
                throw new Exception(error);
            }

            return ChangeState<R>();
        }

        /// <summary>
        ///     changes the current state
        /// </summary>
        public R ChangeState<R>() where R: TBaseState
        {
            // avoid changing to the same state
            Type newType = typeof(R);

            if (CurrentState.GetType() == newType)
                return (R)CurrentState;

            // only call end if we have a currentState
            CurrentState.End();

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
            CurrentState.Begin();
            ElapsedTimeInState = 0f;

            // fire the changed event if we have a listener
            OnStateChanged?.Invoke();

            return (R)CurrentState;
        }
    }
}
