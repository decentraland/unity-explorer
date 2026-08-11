using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Analyzers.Tests
{
    public class StructuralChangeAfterRefTests
    {
        private const string ARCH_STUB = @"
namespace Arch.Core
{
    public struct Entity { }

    internal static class Storage<T> { public static T Value; }

    public class World
    {
        public ref T Get<T>(Entity entity) => ref Storage<T>.Value;
        public ref T TryGetRef<T>(Entity entity, out bool exists) { exists = true; return ref Storage<T>.Value; }
        public void Add<T>(Entity entity) { }
        public void Add<T>(Entity entity, T component) { }
        public void Remove<T>(Entity entity) { }
        public void Destroy(Entity entity) { }
        public Entity Create() => default;
    }

    public class CommandBuffer
    {
        public void Add<T>(Entity entity) { }
        public void Remove<T>(Entity entity) { }
    }
}

public struct Movement { public float Speed; }
public struct Tag { }
";

        private static Task VerifyAsync(string body, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<StructuralChangeAfterRefAnalyzer, DefaultVerifier>
            {
                TestCode = ARCH_STUB + @"
public class SomeSystem
{
    private readonly Arch.Core.World world = new Arch.Core.World();
    private readonly Arch.Core.CommandBuffer buffer = new Arch.Core.CommandBuffer();
    private readonly int mode = 0;

    public void Update(Arch.Core.Entity entity)
    {
" + body + @"
    }
}",
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        [Test]
        public Task ReportsUseAfterStructuralChange() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        world.Add<Tag>(entity);
        {|DCLA001:movement|}.Speed = 1f;
");

        [Test]
        public Task ReportsUseAfterDestroy() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        world.Destroy(entity);
        float s = {|DCLA001:movement|}.Speed;
");

        [Test]
        public Task ReportsForTryGetRef() =>
            VerifyAsync(@"
        ref var movement = ref world.TryGetRef<Movement>(entity, out bool exists);
        world.Remove<Tag>(entity);
        {|DCLA001:movement|}.Speed = 2f;
");

        [Test]
        public Task CleanWhenUseCompletesBeforeStructuralChange() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        movement.Speed = 1f;
        world.Add<Tag>(entity);
");

        [Test]
        public Task CleanForPlainCopy() =>
            VerifyAsync(@"
        var movement = world.Get<Movement>(entity);
        world.Add<Tag>(entity);
        movement.Speed = 1f;
");

        [Test]
        public Task CleanForTryGetRefBranchIdiom() =>
            VerifyAsync(@"
        ref var movement = ref world.TryGetRef<Movement>(entity, out bool has);
        if (!has)
            world.Add<Movement>(entity);
        else
            movement.Speed = 1f;
");

        [Test]
        public Task CleanWhenUseIsInsideStructuralCallArguments() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        world.Add(entity, new Movement { Speed = movement.Speed });
");

        [Test]
        public Task CleanForExclusiveSwitchSections() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        switch (mode)
        {
            case 0:
                world.Destroy(entity);
                break;
            case 1:
                movement.Speed = 2f;
                break;
        }
");

        [Test]
        public Task CleanWhenRefIsReacquiredAfterStructuralChange() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        world.Add<Tag>(entity);
        movement = ref world.Get<Movement>(entity);
        movement.Speed = 1f;
");

        [Test]
        public Task ReportsUseBetweenStructuralChangeAndRefetch() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        world.Add<Tag>(entity);
        {|DCLA001:movement|}.Speed = 1f;
        movement = ref world.Get<Movement>(entity);
        movement.Speed = 2f;
");

        [Test]
        public Task ReportsWhenStructuralAndUseShareABranch() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        if (mode == 0)
        {
            world.Add<Tag>(entity);
            {|DCLA001:movement|}.Speed = 1f;
        }
");

        [Test]
        public Task CleanForCommandBufferStructuralChange() =>
            VerifyAsync(@"
        ref var movement = ref world.Get<Movement>(entity);
        buffer.Add<Tag>(entity);
        movement.Speed = 1f;
");
    }
}
