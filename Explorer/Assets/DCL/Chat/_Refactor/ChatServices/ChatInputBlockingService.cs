using DCL.CharacterCamera;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using ECS.Abstract;
using Utility.Arch;
using World = Arch.Core.World;

public class ChatInputBlockingService
{
    private readonly IInputBlock inputBlock;
    private readonly World world;
    private SingleInstanceEntity cameraEntity;

    public ChatInputBlockingService(IInputBlock inputBlock, World world)
    {
        this.inputBlock = inputBlock;
        this.world = world;
    }

    public void Initialize()
    {
        cameraEntity = world.CacheCamera();
    }

    /// <summary>
    /// Blocks player movement and camera controls.
    /// </summary>
    public void Block()
    {
        if (world.IsAlive(cameraEntity))
        {
            world.AddOrGet(cameraEntity, new CameraBlockerComponent());
        }
        inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
    }

    /// <summary>
    /// Re-enables player movement and camera controls.
    /// </summary>
    public void Unblock()
    {
        if (world.IsAlive(cameraEntity))
        {
            world.TryRemove<CameraBlockerComponent>(cameraEntity);
        }
        inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
    }
}