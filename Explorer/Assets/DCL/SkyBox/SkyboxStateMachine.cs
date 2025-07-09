namespace DCL.SkyBox
{
    public class SkyboxStateMachine
    {
        private readonly ISkyboxState[] states;
        private readonly ISkyboxState transition;
        private ISkyboxState? currentState;
        private ISkyboxState? targetState;

        private bool isTransitioning => currentState == transition;

        public SkyboxStateMachine(ISkyboxState[] states,
            ISkyboxState transition)
        {
            this.states = states;
            this.transition = transition;
        }

        public void Update(float dt)
        {
            if (isTransitioning)
            {
                if (transition.Applies())
                    transition.Update(dt);
                else
                {
                    transition.Exit();
                    currentState = targetState;
                    currentState?.Enter();
                    targetState = null;
                }

                return;
            }

            foreach (var state in states)
            {
                if (!state.Applies()) continue;

                if (currentState == state)
                    state.Update(dt);
                else
                {
                    targetState = state;
                    currentState?.Exit();
                    transition.Enter();
                    currentState = transition;
                }

                return;
            }

            // If we reach here, no state applies, including current
            currentState?.Exit();
            currentState = null;
        }
    }
}
