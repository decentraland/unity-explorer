using System;
using System.Collections.Generic;

namespace DCL.Infrastructure.StateMachine
{
    /// <summary>
    /// Base state machine that provides state validation, transitions, and action handling
    /// </summary>
    /// <typeparam name="TState">The state enum type</typeparam>
    public abstract class BaseStateMachine<TState> where TState : struct, Enum
    {
        private TState currentState;
        private readonly bool[,] validTransitions;
        private readonly Action[] stateEnterActions;
        private readonly Action[] stateExitActions;
        private readonly int stateCount;

        protected BaseStateMachine(TState initialState)
        {
            currentState = initialState;

            stateCount = Enum.GetValues(typeof(TState)).Length;
            validTransitions = new bool[stateCount, stateCount];
            stateEnterActions = new Action[stateCount];
            stateExitActions = new Action[stateCount];
        }

        public TState CurrentState => currentState;

        /// <summary>
        /// Initialize the state machine by setting up valid transitions and actions
        /// Override this method to define the state machine behavior
        /// </summary>
        protected virtual void InitializeStateMachine()
        {
            // Override in derived classes
        }
        /// <summary>
        /// Define a valid transition from one state to another
        /// </summary>
        protected void DefineTransition(TState fromState, TState toState)
        {
            var fromIndex = Convert.ToInt32(fromState);
            var toIndex = Convert.ToInt32(toState);
            validTransitions[fromIndex, toIndex] = true;
        }

        /// <summary>
        /// Define an action to execute when entering a state
        /// </summary>
        protected void DefineStateEnterAction(TState state, Action action)
        {
            var index = Convert.ToInt32(state);
            stateEnterActions[index] = action;
        }

        /// <summary>
        /// Define an action to execute when exiting a state
        /// </summary>
        protected void DefineStateExitAction(TState state, Action action)
        {
            var index = Convert.ToInt32(state);
            stateExitActions[index] = action;
        }

        /// <summary>
        /// Attempt to transition to a new state
        /// </summary>
        public bool TransitionTo(TState newState)
        {
            if (EqualityComparer<TState>.Default.Equals(currentState, newState))
            {
                return true;
            }

            var fromIndex = Convert.ToInt32(currentState);
            var toIndex = Convert.ToInt32(newState);

            if (!validTransitions[fromIndex, toIndex])
            {
                return false;
            }

            var exitAction = stateExitActions[fromIndex];
            exitAction?.Invoke();

            // Change state
            var previousState = currentState;
            currentState = newState;

            var enterAction = stateEnterActions[toIndex];
            enterAction?.Invoke();

            OnStateChanged(previousState, newState);
            return true;
        }

        /// <summary>
        /// Check if a transition is valid
        /// </summary>
        public bool IsValidTransition(TState fromState, TState toState)
        {
            var fromIndex = Convert.ToInt32(fromState);
            var toIndex = Convert.ToInt32(toState);
            return validTransitions[fromIndex, toIndex];
        }

        /// <summary>
        /// Get all valid transitions from the current state
        /// </summary>
        public IEnumerable<TState> GetValidTransitions()
        {
            var currentIndex = Convert.ToInt32(currentState);
            var states = Enum.GetValues(typeof(TState));

            for (int i = 0; i < stateCount; i++)
            {
                if (validTransitions[currentIndex, i])
                {
                    yield return (TState)states.GetValue(i)!;
                }
            }
        }

        /// <summary>
        /// Called after a successful state change
        /// </summary>
        protected virtual void OnStateChanged(TState previousState, TState newState)
        {
            // Override in derived classes if needed
        }

        /// <summary>
        /// Force transition to a state without validation (use with caution)
        /// </summary>
        protected void ForceTransition(TState newState)
        {
            var previousState = currentState;
            currentState = newState;
            OnStateChanged(previousState, newState);
        }
    }
}
