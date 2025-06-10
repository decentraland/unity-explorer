using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Utilities;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestMovementSystem : BaseUnityLoopSystem
    {
        private List<Entity> _cachedEntities;
        private float _lastUpdateTime;

        public TestMovementSystem(World world) : base(world)
        {
            _cachedEntities = new List<Entity>();
        }

        protected override void Update(float t)
        {
            World.Query(new QueryDescription().WithAll<LocalTransform, VelocityComponent>(),
                (Entity entity, ref LocalTransform transform, ref VelocityComponent velocity) =>
                {
                    var newList = new List<Vector3>();
                    var transformComponent = transform;
                    transformComponent.Position += velocity.Value * t;

                    _cachedEntities.Add(entity);
                    newList.Add(transformComponent.Position);
                });

            foreach (var entity in _cachedEntities)
            {
                World.Query(new QueryDescription().WithAll<VelocityComponent>(),
                    (ref VelocityComponent velocity) =>
                    {
                        Debug.Log($"Processing velocity {velocity.Value}");
                    });
            }

            World.Query(new QueryDescription().WithAll<LocalTransform>(),
                (Entity entity, ref LocalTransform transform) =>
                {
                    var pos = transform.Position;
                    pos.x += 1.0f;
                });
        }
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestAsyncSystem : BaseUnityLoopSystem
    {
        public TestAsyncSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadDataAsync().Forget();
            ProcessDataAsync();
        }

        private async UniTask LoadDataAsync()
        {
            await SomeAsyncOperation();
        }

        private async UniTaskVoid ProcessDataAsync()
        {
            await AnotherAsyncOperation();
        }

        private async UniTask SomeAsyncOperation()
        {
            await UniTask.Delay(100);
        }

        private async UniTask AnotherAsyncOperation()
        {
            await UniTask.Delay(200);
        }
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestSingletonSystem : BaseUnityLoopSystem
    {
        public TestSingletonSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            World.Query(new QueryDescription().WithAll<InputComponent>(),
                (ref InputComponent input) =>
                {
                    ProcessInput(input);
                });

            var playerQuery = new QueryDescription().WithAll<PlayerComponent>();
            if (World.CountEntities(playerQuery) > 0)
            {
                World.Query(playerQuery, (ref PlayerComponent player) =>
                {
                    UpdatePlayer(player);
                });
            }
        }

        private void ProcessInput(InputComponent input) { }
        private void UpdatePlayer(PlayerComponent player) { }
    }

    public struct TestComponent
    {
        public LocalTransform SDKTransform;
        public ComplexNestedData ComplexData;
        public Dictionary<string, object> DynamicData;
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class testNamingSystem : BaseUnityLoopSystem
    {
        private const float maxSpeed = 10.0f;
        public float publicField;

        public testNamingSystem(World world) : base(world) { }

        private void PrivateMethod() { }

        public void PublicMethod() { }

        private async UniTask LoadData()
        {
            await UniTask.Delay(100);
        }

        protected override void Update(float t)
        {
            PublicMethod();
            PrivateMethod();
            LoadData().Forget();
        }
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestBigSystem : BaseUnityLoopSystem
    {
        public TestBigSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            HandleMovement(t);
            HandleCombat(t);
            HandleInventory(t);
            HandleUI(t);
            HandleAudio(t);
            HandleNetworking(t);
            HandlePhysics(t);
            HandleAnimation(t);
            HandleParticles(t);
            HandleLighting(t);
            HandleTerrain(t);
            HandleWeather(t);
            HandleAI(t);
            HandleInput(t);
            HandleCamera(t);
            HandleSaveLoad(t);
            HandleSettings(t);
            HandleAchievements(t);
            HandleAnalytics(t);
            HandlePurchases(t);
        }

        private void HandleMovement(float t)
        {
            var entities = new List<Entity>();
            World.Query(new QueryDescription().WithAll<LocalTransform>(),
                (Entity entity, ref LocalTransform transform) =>
                {
                    entities.Add(entity);
                    var transformData = transform;
                    transformData.Position += new Vector3(1, 0, 0) * t;
                });

            foreach (var entity in entities)
            {
                World.Add<MovedTag>(entity);
            }
        }

        private void HandleCombat(float t)
        {
            World.Query(new QueryDescription().WithAll<HealthComponent>(),
                (Entity entity, ref HealthComponent health) =>
                {
                    var healthData = health;
                    healthData.Value -= 1;
                });
        }

        private void HandleInventory(float t) { }
        private void HandleUI(float t) { }
        private void HandleAudio(float t) { }
        private void HandleNetworking(float t) { }
        private void HandlePhysics(float t) { }
        private void HandleAnimation(float t) { }
        private void HandleParticles(float t) { }
        private void HandleLighting(float t) { }
        private void HandleTerrain(float t) { }
        private void HandleWeather(float t) { }
        private void HandleAI(float t) { }
        private void HandleInput(float t) { }
        private void HandleCamera(float t) { }
        private void HandleSaveLoad(float t) { }
        private void HandleSettings(float t) { }
        private void HandleAchievements(float t) { }
        private void HandleAnalytics(float t) { }
        private void HandlePurchases(float t) { }
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestComponentMutationSystem : BaseUnityLoopSystem
    {
        public TestComponentMutationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            World.Query(new QueryDescription().WithAll<LocalTransform, VelocityComponent>(),
                (ref LocalTransform transform, ref VelocityComponent velocity) =>
                {
                    var transformValue = transform;
                    transformValue.Position += velocity.Value * t;

                    var currentVelocity = velocity;
                    currentVelocity.Value *= 0.99f;
                });
        }
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestCleanupSystem : BaseUnityLoopSystem
    {
        private readonly ComponentPoolProvider<ExpensiveComponent> _pool;

        public TestCleanupSystem(World world, ComponentPoolProvider<ExpensiveComponent> pool) : base(world)
        {
            _pool = pool;
        }

        protected override void Update(float t)
        {
            World.Query(new QueryDescription().WithAll<ExpensiveComponent, DeleteEntityIntention>(),
                (Entity entity, ref ExpensiveComponent component) =>
                {
                    var data = component;
                    data.ProcessedData = null;
                });
        }
    }

    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class TestTeleportSystem : BaseUnityLoopSystem
    {
        private List<Entity> teleportingEntities;

        public TestTeleportSystem(World world) : base(world)
        {
            teleportingEntities = new List<Entity>();
        }

        protected override void Update(float t)
        {
            World.Query(new QueryDescription().WithAll<PlayerTeleportIntent>(),
                (Entity entity, ref PlayerTeleportIntent teleportIntent, ref CharacterController controller,
                 ref CharacterPlatformComponent platformComponent, ref CharacterRigidTransform rigidTransform) =>
                {
                    teleportingEntities.Add(entity);

                    AsyncLoadProcessReport loadReport = teleportIntent.AssetsResolution;

                    if (loadReport == null)
                    {
                        var controllerData = controller;
                        controllerData.transform.position = teleportIntent.Position;

                        var rigidData = rigidTransform;
                        rigidData.IsGrounded = false;

                        var platformData = platformComponent;
                        platformData.CurrentPlatform = null;
                    }
                    else
                    {
                        ProcessTeleportAsync(entity, teleportIntent, controller).Forget();
                    }
                });

            foreach (var entity in teleportingEntities)
            {
                World.Query(new QueryDescription().WithAll<PlayerTeleportIntent.JustTeleported>(),
                    (ref PlayerTeleportIntent.JustTeleported justTeleported) =>
                    {
                        if (justTeleported.ExpireFrame <= UnityEngine.Time.frameCount)
                        {
                            World.Remove<PlayerTeleportIntent.JustTeleported>(entity);
                        }
                    });
            }
        }

        private async UniTask ProcessTeleportAsync(Entity entity, PlayerTeleportIntent intent, CharacterController controller)
        {
            await intent.AssetsResolution!.WaitUntilFinishedAsync();
            controller.transform.position = intent.Position;
        }
    }

    public struct LocalTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    public struct VelocityComponent
    {
        public Vector3 Value;
    }

    public struct MovedTag { }

    public struct HealthComponent
    {
        public float Value;
    }

    public struct ExpensiveComponent
    {
        public object ProcessedData;
    }

    public struct DeleteEntityIntention { }

    public struct InputComponent
    {
        public Vector2 Movement;
    }

    public struct PlayerComponent
    {
        public string Name;
    }

    public class ComplexNestedData
    {
        public Dictionary<string, List<object>> NestedData;
    }

    public class ComponentPoolProvider<T>
    {
        public T Get() => default(T);
        public void Return(T item) { }
    }
}
