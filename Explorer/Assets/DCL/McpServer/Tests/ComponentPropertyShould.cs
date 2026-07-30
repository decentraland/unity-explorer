#if MCP_TEST_AUTOMATION
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class ComponentPropertyShould
    {
        private enum Corner
        {
            TopLeft,
            BottomRight,
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")] // the members are read by name through ComponentProperty's reflection
        private struct Box
        {
            public float Width { get; set; }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")] // the members are read by name through ComponentProperty's reflection
        private class Nested
        {
            public int Width { get; set; } = 42;

            public string? Missing => null;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")] // the members are read by name through ComponentProperty's reflection
        [SuppressMessage("ReSharper", "NotAccessedField.Compiler")]
        private class Target
        {
            public bool Enabled = true;

            public readonly string Frozen = "no";

            public string ItemId => "urn:emote";

            public Nested Rect { get; } = new ();

            public Nested? Absent => null;

            public Corner Anchor { get; set; } = Corner.TopLeft;

            public float Alpha { get; set; } = 0.5f;

            public string Label { get; set; } = "before";

            public string Sealed { get; } = "immutable";

            public Box Boxed => default(Box);
        }

        private const string HOST = "PropertyHost";

        private Target target = null!;

        /// <summary>Created only by the component-lookup tests; the property walk itself needs no live object.</summary>
        private GameObject? host;

        [SetUp]
        public void SetUp()
        {
            target = new Target();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                Object.DestroyImmediate(host);
        }

        [Test]
        public void FindAComponentByEitherOfItsNamesWhateverTheCasing()
        {
            host = new GameObject(HOST, typeof(BoxCollider));

            Assert.That(ComponentProperty.TryFindComponent(host, "boxcollider", out Component? byName, out string _), Is.True);
            Assert.That(byName, Is.TypeOf<BoxCollider>());

            Assert.That(ComponentProperty.TryFindComponent(host, "unityengine.BoxCollider", out Component? byFullName, out string _), Is.True);
            Assert.That(byFullName, Is.EqualTo(byName), "both names must name the same component");

            // The Transform every object carries is addressed exactly like a component someone added.
            Assert.That(ComponentProperty.TryFindComponent(host, "TRANSFORM", out Component? implicitOne, out string _), Is.True);
            Assert.That(implicitOne, Is.TypeOf<Transform>());
        }

        [Test]
        public void ListWhatTheObjectDoesCarryWhenTheComponentIsMissing()
        {
            host = new GameObject(HOST, typeof(BoxCollider));

            Assert.That(ComponentProperty.TryFindComponent(host, nameof(Rigidbody), out Component? _, out string error), Is.False);

            // A wrong guess is the common case, so the message has to name the object, the guess and the alternatives.
            Assert.That(error, Does.Contain(HOST));
            Assert.That(error, Does.Contain(nameof(Rigidbody)));
            Assert.That(error, Does.Contain($"{nameof(Transform)}, {nameof(BoxCollider)}"));
        }

        [Test]
        public void ReadPropertiesFieldsDottedPathsAndNullLeaves()
        {
            Assert.That(ComponentProperty.TryRead(target, "ItemId", out object? property, out string _), Is.True);
            Assert.That(property, Is.EqualTo("urn:emote"));

            Assert.That(ComponentProperty.TryRead(target, "Enabled", out object? field, out string _), Is.True);
            Assert.That(field, Is.EqualTo(true));

            Assert.That(ComponentProperty.TryRead(target, "Rect.Width", out object? nested, out string _), Is.True);
            Assert.That(nested, Is.EqualTo(42));

            Assert.That(ComponentProperty.TryRead(target, "Rect.Missing", out object? missing, out string _), Is.True);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void ExplainAnUnknownMemberAndANullIntermediateStep()
        {
            Assert.That(ComponentProperty.TryRead(target, "Nope", out object? _, out string unknown), Is.False);
            Assert.That(unknown, Does.Contain("Nope"));

            Assert.That(ComponentProperty.TryRead(target, "Absent.Width", out object? _, out string nullStep), Is.False);
            Assert.That(nullStep, Does.Contain("null"));

            Assert.That(ComponentProperty.TryRead(target, string.Empty, out object? _, out string empty), Is.False);
            Assert.That(empty, Is.Not.Empty);
        }

        [Test]
        public void WriteAPropertyAFieldAndANestedProperty()
        {
            Assert.That(ComponentProperty.TryWrite(target, "Label", "after", out object? _, out string _), Is.True);
            Assert.That(target.Label, Is.EqualTo("after"));

            Assert.That(ComponentProperty.TryWrite(target, "Enabled", false, out object? _, out string _), Is.True);
            Assert.That(target.Enabled, Is.False);

            Assert.That(ComponentProperty.TryWrite(target, "Rect.Width", 7, out object? _, out string _), Is.True);
            Assert.That(target.Rect.Width, Is.EqualTo(7));
        }

        [Test]
        public void ConvertTheWrittenValueToTheMembersType()
        {
            Assert.That(ComponentProperty.TryWrite(target, "Alpha", 1, out object? _, out string _), Is.True);
            Assert.That(target.Alpha, Is.EqualTo(1f));

            Assert.That(ComponentProperty.TryWrite(target, "Anchor", "bottomright", out object? _, out string _), Is.True);
            Assert.That(target.Anchor, Is.EqualTo(Corner.BottomRight));

            Assert.That(ComponentProperty.TryWrite(target, "Anchor", 0, out object? _, out string _), Is.True);
            Assert.That(target.Anchor, Is.EqualTo(Corner.TopLeft));
        }

        [Test]
        public void RefuseAWriteThatWouldNotStick()
        {
            Assert.That(ComponentProperty.TryWrite(target, "Sealed", "x", out object? _, out string noSetter), Is.False);
            Assert.That(noSetter, Does.Contain("setter"));

            Assert.That(ComponentProperty.TryWrite(target, "Frozen", "x", out object? _, out string readOnly), Is.False);
            Assert.That(readOnly, Does.Contain("read-only"));

            // Boxed is read out by value, so reflection would only mutate the copy.
            Assert.That(ComponentProperty.TryWrite(target, "Boxed.Width", 3, out object? _, out string boxed), Is.False);
            Assert.That(boxed, Does.Contain("struct"));

            Assert.That(ComponentProperty.TryWrite(target, "Alpha", "not a number", out object? _, out string unconvertible), Is.False);
            Assert.That(unconvertible, Is.Not.Empty);

            Assert.That(ComponentProperty.TryWrite(target, "Alpha", null, out object? _, out string nullToStruct), Is.False);
            Assert.That(nullToStruct, Does.Contain("null"));

            Assert.That(ComponentProperty.TryConvert(new JObject(), typeof(Nested), out object? _, out string unsupported), Is.False);
            Assert.That(unsupported, Does.Contain(nameof(Nested)));
        }

        [Test]
        public void KeepPrimitivesTypedAndStringifyTheRest()
        {
            Assert.That(ComponentProperty.ToToken(true).Type, Is.EqualTo(JTokenType.Boolean));
            Assert.That(ComponentProperty.ToToken(7).Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(ComponentProperty.ToToken(0.5f).Type, Is.EqualTo(JTokenType.Float));
            Assert.That(ComponentProperty.ToToken("text").Type, Is.EqualTo(JTokenType.String));
            Assert.That(ComponentProperty.ToToken(null).Type, Is.EqualTo(JTokenType.Null));
            Assert.That(ComponentProperty.ToToken(Corner.TopLeft).Value<string>(), Is.EqualTo(nameof(Corner.TopLeft)));
        }
    }
}
#endif
