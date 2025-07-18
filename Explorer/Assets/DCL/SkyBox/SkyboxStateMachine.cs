namespace DCL.SkyBox
{
    public class SkyboxStateMachine
    {
        private readonly ISkyboxState[] states;
        private ISkyboxState? currentState;

        public SkyboxStateMachine(ISkyboxState[] states)
        {
            this.states = states;
        }

        public void Update(float dt)
        {
            foreach (var state in states)
            {
                if (!state.Applies()) continue;

                if (currentState == state)
                    state.Update(dt);
                else
                {
                    currentState?.Exit();
                    currentState = state;
                    currentState.Enter();
                }

                return;
            }

            // If we reach here, no state applies, including current
            currentState?.Exit();
            currentState = null;
        }
    }
}
