#if MCP_TEST_AUTOMATION
using System;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Opts one public static method into <c>call_static_method</c>. A method without this attribute never
    ///     resolves, whatever its type or assembly, so the reachable surface is exactly what the client chose to
    ///     expose rather than every static in the process.
    ///     <para>
    ///         Mark purpose-built test hooks only. Anything reachable this way can be invoked with caller-chosen
    ///         arguments by whatever can post to the local MCP endpoint, so a marked method must not return a secret,
    ///         read or write files, or end the process.
    ///     </para>
    ///     <para>
    ///         It lives in <c>DCL.McpServer</c>, so it is applicable only from an assembly allowed to reference
    ///         <c>DCL.McpServer</c> — not from one <c>DCL.McpServer</c> itself references, which would be a cycle. A
    ///         hook needs no incoming reference of its own: <c>call_static_method</c> finds it reflectively.
    ///     </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class McpCallableAttribute : Attribute
    {
    }
}
#endif
