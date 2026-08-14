using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DCL.Analyzers.Tests
{
    public class AllocationInSystemUpdateTests
    {
        private const string SYSTEM_STUB = @"
namespace ECS.Abstract
{
    public abstract class BaseUnityLoopSystem
    {
        protected abstract void Update(float t);
    }
}
";

        private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<AllocationInSystemUpdateAnalyzer, DefaultVerifier>
            {
                TestCode = "using System.Linq;\n" + SYSTEM_STUB + source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        private static Task VerifySystemAsync(string fields, string updateBody) =>
            VerifyAsync(@"
public class MoveSystem : ECS.Abstract.BaseUnityLoopSystem
{
" + fields + @"
    protected override void Update(float t)
    {
" + updateBody + @"
    }
}");

        [Test]
        public Task ReportsAllocationInHotPathMethod() =>
            VerifyAsync(@"
namespace Utility { public sealed class HotPathAttribute : System.Attribute { } }

public class UrlResolver
{
    private System.Collections.Generic.List<string> parts;

    [Utility.HotPath]
    public void Resolve()
    {
        parts = {|DCLA003:new System.Collections.Generic.List<string>()|};
    }

    public void ColdSetup()
    {
        parts = new System.Collections.Generic.List<string>();
    }
}");

        [Test]
        public Task CleanForAllocationFreeHotPath() =>
            VerifyAsync(@"
namespace Utility { public sealed class HotPathAttribute : System.Attribute { } }

public class UrlResolver
{
    private int count;

    [Utility.HotPath]
    public void Resolve()
    {
        count++;
    }
}");

        [Test]
        public Task CleanForExceptionConstructionOutsideThrow() =>
            VerifySystemAsync(@"
    private System.Exception stored;

    private class LoadFailedException : System.Exception { }
", @"
        stored = new LoadFailedException();
");

        [Test]
        public Task ReportsReferenceTypeCreation() =>
            VerifySystemAsync(@"
    private System.Collections.Generic.List<int> list;
", @"
        list = {|DCLA003:new System.Collections.Generic.List<int>()|};
        list.Clear();
");

        [Test]
        public Task ReportsArrayCreationInDerivedChain() =>
            VerifyAsync(@"
public abstract class MiddleSystem : ECS.Abstract.BaseUnityLoopSystem { }

public class BufferSystem : MiddleSystem
{
    private float[] buffer;
    private int[] ids;

    protected override void Update(float t)
    {
        buffer = {|DCLA003:new float[16]|};
        ids = {|DCLA003:new[] { 1, 2, 3 }|};
        buffer[0] = t;
    }
}");

        [Test]
        public Task ReportsCapturingLambdas() =>
            VerifySystemAsync(@"
    private int total;
    private System.Action action;
", @"
        action = {|DCLA003:() => total += 1|};
        action = {|DCLA003:() => System.Console.WriteLine(t)|};
");

        [Test]
        public Task ReportsStringInterpolation() =>
            VerifySystemAsync(@"
    private string label;
", @"
        label = {|DCLA003:$""t={t}""|};
");

        [Test]
        public Task ReportsStringConcatenation() =>
            VerifySystemAsync(@"
    private string label;
    private string name = ""n"";
", @"
        label = {|DCLA003:""t="" + name|};
");

        [Test]
        public Task ReportsChainedConcatenationOnceAtTheTop() =>
            VerifySystemAsync(@"
    private string label;
    private string name = ""n"";
", @"
        label = {|DCLA003:""a"" + name + ""b""|};
");

        [Test]
        public Task ReportsLinqInvocations() =>
            VerifySystemAsync(@"
    private int[] items = new int[4];
    private System.Linq.IQueryable<int> query;
    private int count;
    private System.Collections.Generic.IEnumerable<int> positives;
", @"
        count = {|DCLA003:items.Count()|};
        positives = {|DCLA003:items.Where(x => x > 0)|};
        count = {|DCLA003:System.Linq.Queryable.Count(query)|};
");

        [Test]
        public Task CleanForAllShapesOutsideUpdate() =>
            VerifyAsync(@"
public class MoveSystem : ECS.Abstract.BaseUnityLoopSystem
{
    private int total;
    private string label;
    private int[] items = new int[4];
    private System.Action action;

    protected override void Update(float t)
    {
        total++;
    }

    public void Prepare(float t)
    {
        var list = new System.Collections.Generic.List<int>();
        var buffer = new float[16];
        action = () => total += 1;
        label = $""t={t}"" + label;
        total = System.Linq.Enumerable.Count(items);
    }
}");

        [Test]
        public Task CleanForNonSystemOverrideWithSystemTypePresent() =>
            VerifyAsync(@"
public abstract class NotASystem
{
    protected virtual void Update(float t) { }
}

public class Widget : NotASystem
{
    private string label;

    protected override void Update(float t)
    {
        var list = new System.Collections.Generic.List<int>();
        label = $""t={t}"";
    }
}");

        [Test]
        public Task CleanWhenNoSystemTypeInCompilation()
        {
            var test = new CSharpAnalyzerTest<AllocationInSystemUpdateAnalyzer, DefaultVerifier>
            {
                TestCode = @"
public abstract class NotASystem
{
    protected virtual void Update(float t) { }
}

public class Widget : NotASystem
{
    private string label;

    protected override void Update(float t)
    {
        var list = new System.Collections.Generic.List<int>();
        label = $""t={t}"";
    }
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task CleanForStructCreationAndNameof() =>
            VerifySystemAsync(@"
    private int total;
", @"
        var span = new System.TimeSpan(1);
        string n = nameof(Update);
        total = span.Seconds + n.Length;
");

        [Test]
        public Task CleanForNonCapturingAndStaticLambdas() =>
            VerifySystemAsync(@"
    private System.Func<int, int> func;
", @"
        func = x => x + 1;
        func = static x => x * 2;
");

        [Test]
        public Task CleanForConstantFoldedConcat() =>
            VerifySystemAsync(@"
    private string label;
", @"
        const string prefix = ""p:"";
        label = ""a"" + ""b"";
        label = prefix + ""c"";
        label = nameof(Update) + ""!"";
");

        [Test]
        public Task CleanForExceptionCreationOnThrowPaths() =>
            VerifySystemAsync(@"
    private int total;
", @"
        if (total < 0)
            throw new System.InvalidOperationException(""negative"");

        int next = total > 0 ? total + 1 : throw new System.NotImplementedException();
        total = next;
");

        [Test]
        public Task ReportsAllocationInQueryAttributedMethod()
        {
            var test = new CSharpAnalyzerTest<AllocationInSystemUpdateAnalyzer, DefaultVerifier>
            {
                TestCode = @"
namespace ECS.Abstract
{
    public abstract class BaseUnityLoopSystem
    {
        protected abstract void Update(float t);
    }
}

public class QueryAttribute : System.Attribute { }

public partial class MoveSystem : ECS.Abstract.BaseUnityLoopSystem
{
    private string label;

    protected override void Update(float t)
    {
        HandleMove(t);
    }

    [Query]
    private void HandleMove(float t)
    {
        label = {|DCLA003:$""t={t}""|};
    }
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task CleanForImpostorBaseWhenRealAnchorExists()
        {
            var test = new CSharpAnalyzerTest<AllocationInSystemUpdateAnalyzer, DefaultVerifier>
            {
                TestCode = @"
namespace ECS.Abstract
{
    public abstract class BaseUnityLoopSystem
    {
        protected abstract void Update(float t);
    }
}

namespace ThirdParty
{
    public abstract class BaseUnityLoopSystem
    {
        protected abstract void Update(float t);
    }
}

public class Widget : ThirdParty.BaseUnityLoopSystem
{
    private string label;

    protected override void Update(float t)
    {
        label = $""t={t}"";
    }
}",
            };

            return test.RunAsync();
        }

        [Test]
        public Task ReportsInterpolationInExpressionBodiedUpdate() =>
            VerifyAsync(@"
public class LabelSystem : ECS.Abstract.BaseUnityLoopSystem
{
    private string label;

    protected override void Update(float t) => label = {|DCLA003:$""t={t}""|};
}");

        [Test]
        public Task ReportsTargetTypedNewForReferenceType() =>
            VerifySystemAsync(@"
    private System.Collections.Generic.List<int> list;
", @"
        list = {|DCLA003:new()|};
");

        [Test]
        public Task ReportsLinqBehindConditionalAccess() =>
            VerifySystemAsync(@"
    private int[] items = new int[4];
    private object result;
", @"
        result = items?{|DCLA003:.Where(x => x > 0)|};
");

        [Test]
        public Task ReportsStringAppendAssignment() =>
            VerifySystemAsync(@"
    private string label;
    private string name = ""n"";
", @"
        {|DCLA003:label += name|};
");

        [Test]
        public Task ReportsAnonymousObjectCreation() =>
            VerifySystemAsync(@"
    private object cached;
", @"
        cached = {|DCLA003:new { X = 1 }|};
");

        [Test]
        public Task ReportsWhenBaseMatchedBySimpleNameOnly()
        {
            var test = new CSharpAnalyzerTest<AllocationInSystemUpdateAnalyzer, DefaultVerifier>
            {
                TestCode = @"
namespace Stubs
{
    public abstract class BaseUnityLoopSystem
    {
        protected abstract void Update(float t);
    }
}

public class OtherSystem : Stubs.BaseUnityLoopSystem
{
    private System.Collections.Generic.List<int> list;

    protected override void Update(float t)
    {
        list = {|DCLA003:new System.Collections.Generic.List<int>()|};
        list.Clear();
    }
}",
            };

            return test.RunAsync();
        }
    }
}
