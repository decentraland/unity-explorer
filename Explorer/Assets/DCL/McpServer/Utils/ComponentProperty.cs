#if MCP_TEST_AUTOMATION
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Finds a component on a resolved UI element and reads or writes a dotted property path on it by reflection,
    ///     backing the MCP <c>get_component_property</c> / <c>set_component_property</c> tools. Public instance
    ///     properties and fields are followed, matched without regard to case and bounded — at most four steps, and
    ///     never through the identity or preference types — so a component cannot serve as a doorway into unrelated
    ///     client state. Every failure (an unknown component or member, an ambiguous name, a null step, a value that
    ///     will not convert, a write that would be lost, a refused path) comes back as a reader-facing message rather
    ///     than an exception, so the dispatcher never has to turn a typo into a stack trace.
    /// </summary>
    public static class ComponentProperty
    {
        // "rectTransform.rect.width" is the deepest documented read, so four steps leave headroom while keeping the
        // object graph a single path can reach small.
        private const int MAX_PATH_STEPS = 4;

        // IgnoreCase matches how a component is named: both halves of an argument pair forgive case, so an agent
        // never has to know which of the two spellings a given member uses.
        private const BindingFlags MEMBERS = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase;

        private static readonly char[] SEPARATORS = { '.' };

        // Namespace roots this tool refuses to touch: the Web3 identity and account types, whose public members reach
        // the signing keys, and the preference store.
        private static readonly string[] GUARDED_NAMESPACES = { "DCL.Web3", "DCL.Prefs" };

        /// <summary>
        ///     Finds a component of <paramref name="gameObject" /> by its type name or its full type name. On a miss,
        ///     <paramref name="error" /> lists what the object does carry, so a wrong guess names the alternatives.
        /// </summary>
        public static bool TryFindComponent(GameObject gameObject, string componentName, [NotNullWhen(true)] out Component? component, out string error)
        {
            var present = new StringBuilder();

            foreach (Component? candidate in gameObject.GetComponents<Component>())
            {
                if (candidate == null)
                    continue;

                Type type = candidate.GetType();

                if (string.Equals(type.Name, componentName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type.FullName, componentName, StringComparison.OrdinalIgnoreCase))
                {
                    component = candidate;
                    error = string.Empty;
                    return true;
                }

                if (present.Length > 0)
                    present.Append(", ");

                present.Append(type.Name);
            }

            component = null;
            error = $"'{gameObject.name}' has no '{componentName}' component. Its components are: {present}.";
            return false;
        }

        /// <summary>
        ///     Walks <paramref name="propertyPath" /> ("IsLoading", "rectTransform.rect.width") from
        ///     <paramref name="target" />. False with a reader-facing <paramref name="error" /> when a step names no
        ///     public member or names several that differ only by case, when a step before the last one reads null,
        ///     and when the path is longer or reaches further than this tool follows.
        /// </summary>
        public static bool TryRead(object target, string propertyPath, out object? value, out string error)
        {
            value = null;

            if (!TrySplit(propertyPath, out string[] steps, out error))
                return false;

            object? current = target;

            foreach (string step in steps)
            {
                if (!TryStepInto(ref current, step, propertyPath, out error))
                    return false;
            }

            value = current;
            return true;
        }

        /// <summary>
        ///     Writes <paramref name="value" /> to the member <paramref name="propertyPath" /> names, converting the
        ///     JSON token to the member's type. Refuses, rather than silently dropping the write, when the member has
        ///     no setter, when its type is not one this converter handles, or when the last step sits on a struct read
        ///     out of a property — where reflection would only mutate the boxed copy.
        /// </summary>
        public static bool TryWrite(object target, string propertyPath, JToken? value, out object? written, out string error)
        {
            written = null;

            if (!TrySplit(propertyPath, out string[] steps, out error))
                return false;

            object? owner = target;

            for (var i = 0; i < steps.Length - 1; i++)
            {
                if (!TryStepInto(ref owner, steps[i], propertyPath, out error))
                    return false;
            }

            if (owner == null)
            {
                error = $"'{propertyPath}' cannot be written: the object holding '{steps[steps.Length - 1]}' is null.";
                return false;
            }

            if (steps.Length > 1 && owner.GetType().IsValueType)
            {
                error = $"'{propertyPath}' cannot be written: '{steps[steps.Length - 2]}' is a struct read by value, so the write would be lost. Set the whole struct instead.";
                return false;
            }

            return TryWriteMember(owner, steps[steps.Length - 1], value, out written, out error);
        }

        /// <summary>
        ///     Converts a JSON token to <paramref name="type" />, covering what a tool argument can carry: strings,
        ///     booleans, the numeric primitives, enums (by member name or by number) and nullable versions of those.
        ///     Anything richer is refused by name so the caller learns what the member actually wants.
        /// </summary>
        public static bool TryConvert(JToken? token, Type type, out object? value, out string error)
        {
            value = null;
            error = string.Empty;

            Type target = Nullable.GetUnderlyingType(type) ?? type;
            bool nullable = !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

            if (token == null || token.Type == JTokenType.Null)
            {
                if (nullable) return true;

                error = $"null cannot be assigned to {type.Name}.";
                return false;
            }

            try
            {
                if (target == typeof(string))
                {
                    value = token.Value<string>();
                    return true;
                }

                if (target.IsEnum)
                {
                    value = token.Type == JTokenType.Integer
                        ? Enum.ToObject(target, token.Value<long>())
                        : Enum.Parse(target, token.Value<string>()!, true);

                    return true;
                }

                if (target == typeof(bool) || target.IsPrimitive || target == typeof(decimal))
                {
                    value = Convert.ChangeType(token.ToObject<object>(), target);
                    return true;
                }
            }
            catch (Exception e)
            {
                error = $"'{token}' cannot be read as {target.Name}: {e.Message}";
                return false;
            }

            error = $"{target.Name} is not a type this tool can write; only strings, booleans, numbers and enums are supported.";
            return false;
        }

        /// <summary>
        ///     Converts a read value into JSON. Primitives keep their type so an agent can compare them numerically;
        ///     everything else — Unity structs, objects, collections — becomes its string form, which is what a test
        ///     assertion on a component property compares anyway.
        /// </summary>
        public static JToken ToToken(object? value)
        {
            switch (value)
            {
                case null: return JValue.CreateNull();
                case string text: return text;
                case bool flag: return flag;
                case Enum enumeration: return enumeration.ToString();
                case byte or sbyte or short or ushort or int or uint or long: return new JValue(Convert.ToInt64(value));
                case ulong unsigned: return new JValue(unsigned);
                case float or double or decimal: return new JValue(Convert.ToDouble(value));
                default: return value.ToString();
            }
        }

        private static bool TrySplit(string propertyPath, out string[] steps, out string error)
        {
            error = string.Empty;
            steps = propertyPath.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries);

            if (steps.Length == 0)
            {
                error = "The property path is empty.";
                return false;
            }

            if (steps.Length > MAX_PATH_STEPS)
            {
                error = $"'{propertyPath}' names {steps.Length} members; this tool follows at most {MAX_PATH_STEPS}, so a component cannot be walked into unrelated client state.";
                return false;
            }

            return true;
        }

        private static bool TryStepInto(ref object? current, string step, string propertyPath, out string error)
        {
            if (current == null)
            {
                error = $"'{step}' cannot be read: an earlier step of '{propertyPath}' is null.";
                return false;
            }

            return TryReadMember(current, step, out current, out error);
        }

        private static bool TryReadMember(object target, string memberName, out object? value, out string error)
        {
            value = null;

            if (!TryFindMember(target.GetType(), memberName, out MemberInfo? member, out error))
                return false;

            if (IsGuarded(member.DeclaringType) || IsGuarded(MemberType(member)))
            {
                error = $"'{memberName}' cannot be read: it reaches the client's identity or preference state, which these tools do not expose.";
                return false;
            }

            switch (member)
            {
                case PropertyInfo { CanRead: true } property:
                    value = property.GetValue(target);
                    return true;
                case FieldInfo field:
                    value = field.GetValue(target);
                    return true;
                default:
                    error = $"'{member.DeclaringType?.Name}.{memberName}' is write-only.";
                    return false;
            }
        }

        private static bool TryWriteMember(object owner, string memberName, JToken? value, out object? written, out string error)
        {
            written = null;
            Type ownerType = owner.GetType();

            if (!TryFindMember(ownerType, memberName, out MemberInfo? member, out error))
                return false;

            if (IsGuarded(member.DeclaringType) || IsGuarded(MemberType(member)))
            {
                error = $"'{memberName}' cannot be written: it reaches the client's identity or preference state, which these tools do not change.";
                return false;
            }

            switch (member)
            {
                case PropertyInfo { CanWrite: true } property:
                    if (!TryConvert(value, property.PropertyType, out written, out error))
                        return false;

                    property.SetValue(owner, written);
                    return true;
                case FieldInfo { IsInitOnly: false } field:
                    if (!TryConvert(value, field.FieldType, out written, out error))
                        return false;

                    field.SetValue(owner, written);
                    return true;
                default:
                    error = member is PropertyInfo
                        ? $"'{ownerType.Name}.{memberName}' has no setter."
                        : $"'{ownerType.Name}.{memberName}' is a read-only field.";

                    return false;
            }
        }

        /// <summary>
        ///     Finds a property or field declared anywhere in the type's chain, most-derived first; within a type an
        ///     exact-case match wins over a differently-cased one, and a name two members answer to is refused rather
        ///     than resolved by declaration order. Walking the chain with DeclaredOnly instead of asking the whole
        ///     hierarchy at once is what keeps a member hidden by a `new` declaration — common in the Unity UI types
        ///     — from raising AmbiguousMatchException.
        /// </summary>
        private static bool TryFindMember(Type type, string memberName, [NotNullWhen(true)] out MemberInfo? member, out string error)
        {
            member = null;
            error = string.Empty;

            for (Type? current = type; current != null; current = current.BaseType)
            {
                MemberInfo? cased = null;
                var ambiguous = false;

                foreach (MemberInfo candidate in current.GetMember(memberName, MemberTypes.Property | MemberTypes.Field, MEMBERS))
                {
                    // An indexer takes arguments this path cannot supply, so it is not the member being named.
                    if (candidate is PropertyInfo property && property.GetIndexParameters().Length > 0)
                        continue;

                    if (string.Equals(candidate.Name, memberName, StringComparison.Ordinal))
                    {
                        member = candidate;
                        return true;
                    }

                    if (cased == null)
                        cased = candidate;
                    else
                        ambiguous = true;
                }

                if (ambiguous)
                {
                    error = $"'{current.Name}' has more than one public member matching '{memberName}'; spell it exactly as it is declared.";
                    return false;
                }

                if (cased != null)
                {
                    member = cased;
                    return true;
                }
            }

            error = $"'{type.Name}' has no public property or field named '{memberName}'.";
            return false;
        }

        private static Type? MemberType(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo property: return property.PropertyType;
                case FieldInfo field: return field.FieldType;
                default: return null;
            }
        }

        private static bool IsGuarded(Type? type)
        {
            string? space = type?.Namespace;

            if (space == null)
                return false;

            foreach (string guarded in GUARDED_NAMESPACES)
            {
                if (space.StartsWith(guarded, StringComparison.Ordinal)
                    && (space.Length == guarded.Length || space[guarded.Length] == '.'))
                    return true;
            }

            return false;
        }
    }
}
#endif
