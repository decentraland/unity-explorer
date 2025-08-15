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

        public ISkyboxState? CurrentState { get; set; }

        public void Update(float dt)
        {
            foreach (var state in states)
            {
                if (!state.Applies()) continue;

                if (CurrentState == state)
                    state.Update(dt);
                else
                {
                    CurrentState?.Exit();
                    CurrentState = state;
                    CurrentState.Enter();
                }

                return;
            }

            // If we reach here, no state applies, including current
            CurrentState?.Exit();
            CurrentState = null;
        }
    }
}
