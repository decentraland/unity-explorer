namespace DCL.SkyBox
{
    public interface ISkyboxState
    {
        public bool Applies();
        public void Enter();
        public void Update(float dt);
        public void Exit();
    }
}
