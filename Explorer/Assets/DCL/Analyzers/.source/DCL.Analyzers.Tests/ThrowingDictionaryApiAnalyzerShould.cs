using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace DCL.Analyzers.Tests
{
    /// <summary>
    ///     Each source marks the lines expected to be reported with a trailing <c>// expect: DCLxxxx</c> comment.
    /// </summary>
    public class ThrowingDictionaryApiAnalyzerShould
    {
        private const string NATIVE_STUBS = @"
namespace Unity.Collections
{
    public struct NativeHashMap<TKey, TValue>
    {
        public void Add(TKey key, TValue item) { }
        public bool TryAdd(TKey key, TValue item) => true;
        public TValue this[TKey key] { get => default!; set { } }
        public ReadOnly AsReadOnly() => default;
        public struct ReadOnly { public TValue this[TKey key] => default!; }
    }

    public struct NativeParallelHashMap<TKey, TValue>
    {
        public ParallelWriter AsParallelWriter() => default;
        public struct ParallelWriter { public void Add(TKey key, TValue item) { } }
    }

    public struct NativeParallelMultiHashMap<TKey, TValue>
    {
        public void Add(TKey key, TValue item) { }
    }
}";

        private static readonly Regex EXPECT = new (@"// expect: (DCL\d{4})");

        [Test]
        public void ReportAddOnEveryDictionaryShape()
        {
            Verify(@"
using System.Collections.Generic;
using System.Collections.Concurrent;
class C
{
    void M(Dictionary<int, int> d, IDictionary<int, int> id, SortedDictionary<int, int> sd, SortedList<int, int> sl, Custom custom)
    {
        d.Add(1, 1); // expect: DCL0001
        id.Add(1, 1); // expect: DCL0001
        sd.Add(1, 1); // expect: DCL0001
        sl.Add(1, 1); // expect: DCL0001
        custom.Add(1, 1); // expect: DCL0001
        d.TryAdd(1, 1);
        d.Remove(1);
        new List<int>().Add(1);
        new HashSet<int>().Add(1);
    }
}
class Custom : Dictionary<int, int> { }");
        }

        [Test]
        public void ReportIndexerReadsButNotPlainWrites()
        {
            Verify(@"
using System.Collections.Generic;
class C
{
    void M(Dictionary<int, int> d, IDictionary<int, int> id, IReadOnlyDictionary<int, int> rd, Dictionary<int, string> s)
    {
        var a = d[1]; // expect: DCL0002
        var b = id[1]; // expect: DCL0002
        var c = rd[1]; // expect: DCL0002
        d[1] += 1; // expect: DCL0002
        d[1]++; // expect: DCL0002
        s[1] ??= """"; // expect: DCL0002
        Use(d[1]); // expect: DCL0002
        d[1] = 2;
        d[d.Count] = 3;
        id[1] = 2;
        d.TryGetValue(1, out int v);
        d.ContainsKey(1);
        var list = new List<int> { 1 };
        var e = list[0];
    }

    void Use(int i) { }
}");
        }

        [Test]
        public void AllowDeconstructionWritesButReportDeconstructionReads()
        {
            Verify(@"
using System.Collections.Generic;
class C
{
    void M(Dictionary<int, int> d)
    {
        (d[1], d[2]) = (3, 4);
        ((d[1], d[2]), d[3]) = ((5, 6), 7);
        var (a, b) = (d[1], 2); // expect: DCL0002
    }
}");
        }

        [Test]
        public void AllowCollectionAndIndexInitializers()
        {
            Verify(@"
using System.Collections.Generic;
class C
{
    static readonly Dictionary<int, int> A = new () { { 1, 1 } };
    static readonly Dictionary<int, int> B = new () { [1] = 1 };
}");
        }

        [Test]
        public void ReportToDictionary()
        {
            Verify(@"
using System.Linq;
class C
{
    void M(int[] items)
    {
        var d = items.ToDictionary(i => i); // expect: DCL0001
    }
}");
        }

        [Test]
        public void ReportNativeMapsButNotMultiMaps()
        {
            Verify(@"
using Unity.Collections;
class C
{
    void M(NativeHashMap<int, int> map, NativeParallelHashMap<int, int> parallel, NativeParallelMultiHashMap<int, int> multi)
    {
        map.Add(1, 1); // expect: DCL0001
        var a = map[1]; // expect: DCL0002
        var b = map.AsReadOnly()[1]; // expect: DCL0002
        parallel.AsParallelWriter().Add(1, 1); // expect: DCL0001
        map[1] = 2;
        map.TryAdd(1, 1);
        multi.Add(1, 1);
    }
}" + NATIVE_STUBS);
        }

        [Test]
        public void AllowJObjectIndexerReadsButReportJObjectAdd()
        {
            Verify(@"
using Newtonsoft.Json.Linq;
class C
{
    void M(JObject json)
    {
        var a = json[""type""];
        json.Add(""type"", null); // expect: DCL0001
    }
}

namespace Newtonsoft.Json.Linq
{
    using System.Collections;
    using System.Collections.Generic;

    public class JToken { }

    public class JObject : IDictionary<string, JToken?>
    {
        public JToken? this[string key] { get => null; set { } }
        public void Add(string key, JToken? value) { }
        public ICollection<string> Keys => null!;
        public ICollection<JToken?> Values => null!;
        public int Count => 0;
        public bool IsReadOnly => false;
        public bool ContainsKey(string key) => false;
        public bool Remove(string key) => false;
        public bool TryGetValue(string key, out JToken? value) { value = null; return false; }
        public void Add(KeyValuePair<string, JToken?> item) { }
        public void Clear() { }
        public bool Contains(KeyValuePair<string, JToken?> item) => false;
        public void CopyTo(KeyValuePair<string, JToken?>[] array, int arrayIndex) { }
        public bool Remove(KeyValuePair<string, JToken?> item) => false;
        public IEnumerator<KeyValuePair<string, JToken?>> GetEnumerator() => null!;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}");
        }

        [Test]
        public void SkipMapImplementationsForwardingTheThrowingContract()
        {
            Verify(@"
using System.Collections;
using System.Collections.Generic;
class Wrapper : IReadOnlyDictionary<int, int>
{
    private readonly Dictionary<int, int> inner = new ();

    public int this[int key] => inner[key];
    public void Add(int key, int value) => inner.Add(key, value);
    int IReadOnlyDictionary<int, int>.this[int key] => inner[key];
    public int First() => inner[0]; // expect: DCL0002

    public IEnumerable<int> Keys => inner.Keys;
    public IEnumerable<int> Values => inner.Values;
    public int Count => inner.Count;
    public bool ContainsKey(int key) => inner.ContainsKey(key);
    public bool TryGetValue(int key, out int value) => inner.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<int, int>> GetEnumerator() => inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}");
        }

        [TestCase("DCL.EditMode.Tests")]
        [TestCase("ECS.TestSuite")]
        public void SkipTestAssemblies(string assemblyName)
        {
            const string SOURCE = @"
using System.Collections.Generic;
class C
{
    int M(Dictionary<int, int> d) => d[1];
}";

            Assert.That(Analyze(SOURCE, assemblyName: assemblyName), Is.Empty);
        }

        [Test]
        public void HonorPragmaSuppression()
        {
            Verify(@"
using System.Collections.Generic;
class C
{
    void M(Dictionary<int, int> d)
    {
#pragma warning disable DCL0001 // serialization boundary
        d.Add(1, 1);
#pragma warning restore DCL0001
    }
}");
        }

        [TestCase("Library/PackageCache/com.unity.cinemachine@1.0.0/Runtime/Sample.cs")]
        [TestCase("Packages/com.decentraland.sample/Runtime/Sample.cs")]
        [TestCase("../unity-shared-dependencies/Runtime/Sample.cs")]
        public void IgnoreSourcesOutsideAssets(string path)
        {
            const string SOURCE = @"
using System.Collections.Generic;
class C
{
    int M(Dictionary<int, int> d) => d[1];
}";

            Assert.That(Analyze(SOURCE, path), Is.Empty);
        }

        [TestCase("/Users/dev/unity-explorer/Explorer/Assets/DCL/Sample.cs")]
        [TestCase(@"C:\unity-explorer\Explorer\Assets\DCL\Sample.cs")]
        public void ReportAbsoluteAssetsPaths(string path)
        {
            const string SOURCE = @"
using System.Collections.Generic;
class C
{
    int M(Dictionary<int, int> d) => d[1];
}";

            Assert.That(Analyze(SOURCE, path), Has.Length.EqualTo(1));
        }

        private static void Verify(string source)
        {
            var expected = new List<string>();
            string[] lines = source.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                Match match = EXPECT.Match(lines[i]);

                if (match.Success)
                    expected.Add($"{match.Groups[1].Value}@{i + 1}");
            }

            var actual = new List<string>();

            foreach (Diagnostic diagnostic in Analyze(source))
                actual.Add($"{diagnostic.Id}@{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}");

            Assert.That(actual, Is.EquivalentTo(expected));
        }

        private static ImmutableArray<Diagnostic> Analyze(string source, string path = "Assets/DCL/Sample.cs", string assemblyName = "DCL.Sample")
        {
            var metadataReferences = new List<MetadataReference>();

            foreach (string assemblyPath in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
                metadataReferences.Add(MetadataReference.CreateFromFile(assemblyPath));

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9), path) },
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            ImmutableArray<Diagnostic> compileErrors = compilation.GetDiagnostics().RemoveAll(d => d.Severity != DiagnosticSeverity.Error);
            Assert.That(compileErrors, Is.Empty, "sample source must compile");

            return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ThrowingDictionaryApiAnalyzer()))
                              .GetAnalyzerDiagnosticsAsync().Result;
        }
    }
}
