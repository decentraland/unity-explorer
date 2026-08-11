using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Analyzers.Tests
{
    public class FfiEnumUnderlyingTypeTests
    {
        private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<FfiEnumUnderlyingTypeAnalyzer, DefaultVerifier>
            {
                TestCode = "using System.Runtime.InteropServices;\n" + source,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        [Test]
        public Task ReportsBareEnumParameter() =>
            VerifyAsync(@"
public enum Mode { A, B }

public static class Native
{
    [DllImport(""lib"")]
    public static extern void SetMode({|DCLA005:Mode mode|});
}");

        [Test]
        public Task ReportsBareEnumReturnType() =>
            VerifyAsync(@"
public enum Status { Ok, Fail }

public static class Native
{
    [DllImport(""lib"")]
    public static extern {|DCLA005:Status|} GetStatus();
}");

        [Test]
        public Task ReportsBareEnumFieldInStructParameter() =>
            VerifyAsync(@"
public enum Kind { X }

public struct Payload
{
    public int Size;
    public Kind Kind;
}

public static class Native
{
    [DllImport(""lib"")]
    public static extern void Send({|DCLA005:Payload payload|});
}");

        [Test]
        public Task CleanWhenUnderlyingTypeExplicit() =>
            VerifyAsync(@"
public enum Mode : byte { A, B }

public enum Status : int { Ok, Fail }

public static class Native
{
    [DllImport(""lib"")]
    public static extern Status SetMode(Mode mode);
}");

        [Test]
        public Task CleanForBareEnumOutsideFfi() =>
            VerifyAsync(@"
public enum Mode { A, B }

public static class Service
{
    public static void SetMode(Mode mode) { }
}");

        [Test]
        public Task CleanForMetadataEnum() =>
            VerifyAsync(@"
public static class Native
{
    [DllImport(""lib"")]
    public static extern void SetDay(System.DayOfWeek day);
}");
    }
}
