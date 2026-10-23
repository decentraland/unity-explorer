using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace BevyMigration.Tools.ApiInventory.Tests
{
    public class MemberCollectorShould
    {
        // A stand-in for UnityEngine: the collector keys on the namespace root, not on the real assembly.
        private const string FAKE_UNITY = @"
namespace UnityEngine
{
    public class Object { public static bool operator ==(Object a, Object b) => true; public static bool operator !=(Object a, Object b) => false; public override bool Equals(object o) => true; public override int GetHashCode() => 0; }
    public class Component : Object { }
    public class GameObject : Object { public GameObject(string name) { } public T GetComponent<T>() => default; public Transform transform; }
    public class Transform : Component { public Vector3 position { get; set; } }
    public class Renderer : Component { public bool enabled { get; set; } }
    public class Material : Object { public void SetFloat(int id, float value) { } public void SetFloat(string name, float value) { } }
    public struct Vector3 { public float x; public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 zero; }
    public static class Physics { public static bool Raycast(Vector3 origin, out Vector3 hit) { hit = origin; return true; } }
}
namespace Unity.Mathematics { public struct float3 { public float x; } }
namespace TMPro { public class TMP_Text : UnityEngine.Component { public string text { get; set; } } }
namespace UnityEditor { public static class EditorUtility { public static void SetDirty(UnityEngine.Object o) { } } }
namespace Cinemachine { public class CinemachineBrain : UnityEngine.Component { public bool IsBlending { get; } } }
";

        private const string CONSUMER = @"
using UnityEngine;
using Unity.Mathematics;
namespace DCL.Sample
{
    public class Consumer
    {
        public void Run(TMPro.TMP_Text label, Cinemachine.CinemachineBrain brain)
        {
            bool blending = brain.IsBlending;
            var go = new GameObject(""cube"");
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.enabled = false;
            new Material().SetFloat(1, 2f);
            Vector3 sum = go.transform.position + Vector3.zero;
            float3 f = default;
            f.x = 1f;
            label.text = ""hi"";
            bool same = go == null;
            Physics.Raycast(sum, out Vector3 hit);
            UnityEditor.EditorUtility.SetDirty(go);
            System.Console.WriteLine(sum.x);
        }
    }
}
";

        private Dictionary<string, MemberUse> members = null!;

        [SetUp]
        public void SetUp()
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            SyntaxTree fake = CSharpSyntaxTree.ParseText(FAKE_UNITY, parseOptions, "Fake.cs");
            SyntaxTree consumer = CSharpSyntaxTree.ParseText(CONSUMER, parseOptions, "Explorer/Assets/DCL/Sample/Consumer.cs");
            string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

            CSharpCompilation compilation = CSharpCompilation.Create("test",
                new[] { fake, consumer },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")), MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Console.dll")) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            members = new Dictionary<string, MemberUse>();
            var collector = new MemberCollector(compilation.GetSemanticModel(consumer), members, "Explorer/Assets/DCL/Sample/Consumer.cs");
            collector.Visit(consumer.GetRoot());
            Assert.That(collector.Unresolved, Is.EqualTo(0));
        }

        [Test]
        public void NameMembersLikeTheAllowlist()
        {
            Assert.That(members.Keys, Is.SupersetOf(new[]
            {
                "UnityEngine.GameObject",
                "UnityEngine.GameObject..ctor(string)",
                "UnityEngine.GameObject.GetComponent<T>()",
                "UnityEngine.GameObject.transform",
                "UnityEngine.Renderer.enabled",
                "UnityEngine.Material..ctor()",
                "UnityEngine.Material.SetFloat(int,float)",
                "UnityEngine.Transform.position",
                "UnityEngine.Vector3.op_Addition(Vector3,Vector3)",
                "UnityEngine.Vector3.zero",
                "UnityEngine.Vector3.x",
                "UnityEngine.Object.op_Equality(Object,Object)",
                "UnityEngine.Physics.Raycast(Vector3,out Vector3)",
                "Unity.Mathematics.float3.x",
                "TMPro.TMP_Text.text",
                "UnityEditor.EditorUtility.SetDirty(Object)",
                "Cinemachine.CinemachineBrain.IsBlending",
            }));
        }

        [Test]
        public void IgnoreNonUnityAndNamespaceSymbols()
        {
            foreach (string id in members.Keys)
                Assert.That(id, Does.StartWith("UnityEngine.").Or.StartWith("Unity.").Or.StartWith("TMPro.").Or.StartWith("UnityEditor.").Or.StartWith("Cinemachine."));

            Assert.That(members.Keys, Has.None.EqualTo("UnityEngine"));
            Assert.That(members.Keys, Has.None.EqualTo("DCL.Sample.Consumer"));
        }

        [Test]
        public void CountUsesPerFile()
        {
            MemberUse vector3 = members["UnityEngine.Vector3"];

            Assert.That(vector3.Files["Explorer/Assets/DCL/Sample/Consumer.cs"], Is.GreaterThanOrEqualTo(3));
            Assert.That(vector3.Count, Is.EqualTo(vector3.Files["Explorer/Assets/DCL/Sample/Consumer.cs"]));
            Assert.That(members["UnityEngine.Material.SetFloat(int,float)"].Kind, Is.EqualTo("method"));
            Assert.That(members["UnityEngine.GameObject..ctor(string)"].Kind, Is.EqualTo("ctor"));
            Assert.That(members["UnityEngine.Renderer.enabled"].Kind, Is.EqualTo("property"));
        }

        [Test]
        public void GroupMembersBySubsystem()
        {
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.GameObject"), Is.EqualTo("object_model"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.Material"), Is.EqualTo("rendering"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.Rendering.Universal.UniversalRenderPipeline"), Is.EqualTo("rendering"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.ParticleSystem.MainModule"), Is.EqualTo("particles"));
            Assert.That(SubsystemGroups.GroupOf("TMPro", "TMPro.TMP_Text"), Is.EqualTo("ui"));
            Assert.That(SubsystemGroups.GroupOf("Unity", "Unity.Collections.NativeArray<T>"), Is.EqualTo("collections_jobs"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.Something.Unknown"), Is.EqualTo("other"));
            Assert.That(SubsystemGroups.GroupOf("Cinemachine", "Cinemachine.CinemachineBrain"), Is.EqualTo("cinemachine"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.MeshCollider"), Is.EqualTo("physics"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.AudioSpeakerMode"), Is.EqualTo("audio"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.SkinnedMeshRenderer"), Is.EqualTo("rendering"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.TestTools.LogAssert"), Is.EqualTo("testing"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.SerializeField"), Is.EqualTo("object_model"));
            Assert.That(SubsystemGroups.GroupOf("UnityEngine", "UnityEngine.ParticleSystem.MainModule"), Is.EqualTo("particles"));
            Assert.That(SubsystemGroups.GroupOf("UnityEditor", "UnityEditor.EditorUtility"), Is.EqualTo("editor"));
            Assert.That(SubsystemGroups.PhaseOf("ui"), Is.EqualTo("C"));
            Assert.That(SubsystemGroups.PhaseOf("particles"), Is.EqualTo("F"));
            Assert.That(SubsystemGroups.PhaseOf("other"), Is.EqualTo("B"));
        }
    }
}
