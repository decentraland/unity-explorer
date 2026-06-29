#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

internal static class ClaudeIPC
{
    private const string ROOT = "/tmp/dcl-editor";
    private static readonly string CMD = Path.Combine(ROOT, "cmd");
    private static readonly string OUT = Path.Combine(ROOT, "out");
    private static readonly string HEARTBEAT = Path.Combine(ROOT, "heartbeat");

    private static string _projectHeartbeat;

    [InitializeOnLoadMethod]
    private static void Init()
    {
        try
        {
            Directory.CreateDirectory(ROOT);
            Directory.CreateDirectory(CMD);
            Directory.CreateDirectory(OUT);
            TryChmod0700(ROOT);
            TryChmod0700(CMD);
            TryChmod0700(OUT);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[ClaudeIPC] watcher armed at " + ROOT);
        }
        catch (Exception e)
        {
            Debug.LogError("[ClaudeIPC] init failed: " + e);
        }
    }

    private static void TryChmod0700(string path)
    {
        try
        {
            var t = Type.GetType("System.IO.File, System.Runtime");
            var unixModeType = Type.GetType("System.IO.UnixFileMode, System.Runtime");
            var setMode = t?.GetMethod("SetUnixFileMode", new[] { typeof(string), unixModeType });
            if (setMode != null)
            {
                setMode.Invoke(null, new object[] { path, Enum.ToObject(unixModeType, 0x1C0) });
                return;
            }
        }
        catch { }
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("chmod", "0700 \"" + path + "\"")
            { CreateNoWindow = true, UseShellExecute = false };
            System.Diagnostics.Process.Start(psi)?.WaitForExit(2000);
        }
        catch { }
    }

    private static double _nextHeartbeat;

    private static string ProjectHeartbeat()
    {
        if (_projectHeartbeat != null) return _projectHeartbeat;
        string mine = Directory.GetParent(Application.dataPath).FullName.TrimEnd('/');
        var sb = new StringBuilder("hb-");
        foreach (var ch in mine)
            sb.Append(((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) ? ch : '_');
        _projectHeartbeat = Path.Combine(ROOT, sb.ToString());
        return _projectHeartbeat;
    }

    private static void Tick()
    {
        try
        {
            var now = EditorApplication.timeSinceStartup;
            if (now >= _nextHeartbeat)
            {
                string stamp = now.ToString("F1");
                File.WriteAllText(HEARTBEAT, stamp);
                File.WriteAllText(ProjectHeartbeat(), stamp);
                _nextHeartbeat = now + 1.0;
            }

            string[] files;
            try { files = Directory.GetFiles(CMD, "*.json"); }
            catch { return; }
            if (files.Length == 0) return;
            Array.Sort(files);

            foreach (var f in files)
            {
                string id = Path.GetFileNameWithoutExtension(f);
                string body;
                try { body = File.ReadAllText(f); }
                catch { continue; }

                Dictionary<string, string> parsed = null;
                try { parsed = ParseFlat(body); } catch { }

                if (parsed != null && parsed.TryGetValue("project", out var proj) && !string.IsNullOrEmpty(proj))
                {
                    string mine = Directory.GetParent(Application.dataPath).FullName.TrimEnd('/');
                    if (!string.Equals(proj.TrimEnd('/'), mine, StringComparison.Ordinal))
                        continue;
                }

                string response;
                try { response = Dispatch(parsed ?? ParseFlat(body)); }
                catch (Exception e) { response = Err(e.Message); }

                string outPath = Path.Combine(OUT, id + ".json");
                string tmp = outPath + ".tmp";
                File.WriteAllText(tmp, response);
                File.Delete(f);
                if (File.Exists(outPath)) File.Delete(outPath);
                File.Move(tmp, outPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ClaudeIPC] tick: " + e.Message);
        }
    }

    private static string Dispatch(Dictionary<string, string> c)
    {
        if (!c.TryGetValue("op", out var op)) return Err("no op");
        switch (op)
        {
            case "ping":            return Ok("pong");
            case "capabilities":    return Capabilities();
            case "exec":            return Exec(Get(c, "method"), c);
            case "compile":         return DoCompile();
            case "scene-roots":     return SceneRoots();
            case "hierarchy":       return Hierarchy(Get(c, "scope"), IntOr(c, "maxDepth", 40), IntOr(c, "max", 4000));
            case "find-type":       return FindType(Get(c, "type"));
            case "asmdefs":         return AsmDefs();
            case "game-rect":       return GameRect();
            case "screenshot-game": return ScreenshotGame(Get(c, "path"), IntOr(c, "width", 1280), IntOr(c, "height", 720));
            case "screenshot":      return ScreenshotComposited(Get(c, "path"), IntOr(c, "super", 1));
            case "ui-click":        return UiClick(Get(c, "text"), Get(c, "scope"));
            case "ui-list":         return UiList(Get(c, "scope"));
            case "component-get":   return ComponentGet(Get(c, "object"), Get(c, "component"), Get(c, "member"));
            case "component-set":   return ComponentSet(Get(c, "object"), Get(c, "component"), Get(c, "member"), Get(c, "value"));
            case "text":            return TextInput(Get(c, "target"), Get(c, "value"));
            case "world-ready":     return WorldReady();
            case "log":             Debug.Log("[ClaudeIPC] " + Get(c, "msg")); return Ok(true);
            default:                return Err("unknown op: " + op);
        }
    }

    private static string Exec(string fullName, Dictionary<string, string> cmd)
    {
        if (!ExecAllowed()) return Err("exec disabled (set DCL_IPC_ALLOW_EXEC!=0 to enable arbitrary static-method invocation)");
        if (string.IsNullOrEmpty(fullName)) return Err("missing method");
        int dot = fullName.LastIndexOf('.');
        if (dot < 0) return Err("expected Namespace.Class.Method, got " + fullName);

        var t = ResolveType(fullName.Substring(0, dot));
        if (t == null) return Err("type not found: " + fullName.Substring(0, dot));

        string methodName = fullName.Substring(dot + 1);

        var argMap = new Dictionary<string, string>();
        foreach (var kv in cmd)
        {
            if (kv.Key.StartsWith("arg.")) argMap[kv.Key.Substring(4)] = kv.Value;
        }

        var candidates = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(mi => mi.Name == methodName)
            .ToArray();
        if (candidates.Length == 0) return Err("static method not found: " + fullName);

        MethodInfo m = null;
        foreach (var mi in candidates)
        {
            var ps = mi.GetParameters();
            if (ps.Length != argMap.Count) continue;
            if (ps.All(p => argMap.ContainsKey(p.Name))) { m = mi; break; }
        }
        if (m == null)
        {
            foreach (var mi in candidates)
                if (mi.GetParameters().Length == argMap.Count) { m = mi; break; }
        }
        if (m == null)
        {
            var sigs = string.Join("; ", candidates.Select(mi =>
                methodName + "(" + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")"));
            return Err("no overload matches " + argMap.Count + " arg(s); candidates: " + sigs);
        }

        var parameters = m.GetParameters();
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (!argMap.TryGetValue(p.Name, out var raw))
            {
                raw = argMap.Count > i ? argMap.Values.ElementAt(i) : null;
                if (raw == null) return Err("missing arg." + p.Name + " for " + fullName);
            }
            try { args[i] = ParseArg(raw, p.ParameterType); }
            catch (Exception e) { return Err("arg." + p.Name + " parse error: " + e.Message); }
        }

        object ret;
        try { ret = m.Invoke(null, args); }
        catch (TargetInvocationException tie) { return Err("invoke threw: " + (tie.InnerException ?? tie)); }
        catch (Exception e) { return Err("invoke threw: " + e); }

        if (m.ReturnType == typeof(void))
            return Ok("invoked " + fullName + (parameters.Length == 0 ? "" : "(" + string.Join(", ", args.Select(a => a == null ? "null" : a.ToString())) + ")"));
        return "{\"ok\":true,\"result\":" + JsonValue(ret) + "}";
    }

    private static object ParseArg(string raw, Type target)
    {
        if (target == typeof(string)) return raw;
        if (target == typeof(int))    return int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(long))   return long.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(short))  return short.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(byte))   return byte.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(float))  return float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(double)) return double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        if (target == typeof(bool))
        {
            if (raw == "1" || raw.Equals("true",  StringComparison.OrdinalIgnoreCase)) return true;
            if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            throw new ArgumentException("expected true/false/1/0, got " + raw);
        }
        if (target.IsEnum) return Enum.Parse(target, raw, ignoreCase: true);
        throw new ArgumentException("unsupported parameter type: " + target.FullName);
    }

    private static string DoCompile()
    {

        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
        return Ok("compile requested");
    }

    private static string SceneRoots()
    {
        var sb = new StringBuilder("{\"ok\":true,\"result\":[");
        bool first = true;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            foreach (var go in s.GetRootGameObjects())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"scene\":").Append(JsonString(s.name))
                  .Append(",\"name\":").Append(JsonString(go.name))
                  .Append(",\"active\":").Append(go.activeInHierarchy ? "true" : "false")
                  .Append(",\"components\":[");
                bool fc = true;
                foreach (var c in go.GetComponents<Component>())
                {
                    if (!fc) sb.Append(',');
                    fc = false;
                    sb.Append(JsonString(c == null ? "<missing>" : c.GetType().FullName));
                }
                sb.Append("]}");
            }
        }
        return sb.Append("]}").ToString();
    }

    private static string FindType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return Err("missing type");
        var t = ResolveType(typeName);
        if (t == null) return Err("type not found: " + typeName);

        var sb = new StringBuilder("{\"ok\":true,\"result\":{\"type\":")
            .Append(JsonString(t.FullName)).Append(",\"assembly\":")
            .Append(JsonString(t.Assembly.GetName().Name)).Append(",\"instances\":[");

        if (typeof(UnityEngine.Object).IsAssignableFrom(t))
        {
            var found = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(JsonString(found[i].name))
                  .Append(",\"id\":").Append(found[i].GetEntityId().ToString()).Append('}');
            }
        }
        return sb.Append("]}}").ToString();
    }

    private static string WorldReady()
    {
        bool playing = EditorApplication.isPlaying;
        bool compiling = EditorApplication.isCompiling || EditorApplication.isUpdating;

        bool world = false;
        int plugins = -1;

        if (playing)
        {
            try
            {
                MonoBehaviour msl = UnityEngine.Object
                    .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                    .FirstOrDefault(mb => mb.GetType().Name == "MainSceneLoader");

                if (msl != null)
                {
                    object dwc = msl.GetType()
                        .GetField("dynamicWorldContainer", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(msl);

                    if (dwc != null)
                    {
                        world = true;
                        var enumerable = dwc.GetType().GetProperty("GlobalPlugins")?.GetValue(dwc) as System.Collections.IEnumerable;
                        if (enumerable != null)
                        {
                            plugins = 0;
                            foreach (var _ in enumerable) plugins++;
                        }
                    }
                }
            }
            catch (Exception e) { return Err("world-ready probe threw: " + e.Message); }
        }

        return "{\"ok\":true,\"result\":{\"playing\":" + (playing ? "true" : "false")
             + ",\"compiling\":" + (compiling ? "true" : "false")
             + ",\"world\":" + (world ? "true" : "false")
             + ",\"plugins\":" + plugins + "}}";
    }

    private static string AsmDefs()
    {
        var asms = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
        var sb = new StringBuilder("{\"ok\":true,\"result\":[");
        for (int i = 0; i < asms.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"name\":").Append(JsonString(asms[i].name))
              .Append(",\"output\":").Append(JsonString(asms[i].outputPath))
              .Append(",\"sourceCount\":").Append(asms[i].sourceFiles.Length).Append('}');
        }
        return sb.Append("]}").ToString();
    }

    private static string GameRect()
    {
        var t = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(x => x.FullName == "UnityEditor.GameView");
        if (t == null) return Err("GameView type not found");
        var win = EditorWindow.GetWindow(t, false, null, false);
        if (win == null) return Err("no GameView open");
        var p = win.position;
        return "{\"ok\":true,\"result\":{\"x\":" + (int)p.x + ",\"y\":" + (int)p.y
             + ",\"w\":" + (int)p.width + ",\"h\":" + (int)p.height + "}}";
    }

    private static string ScreenshotGame(string path, int w, int h)
    {
        if (string.IsNullOrEmpty(path)) return Err("missing path");
        if (w <= 0 || h <= 0) return Err("invalid dimensions");
        var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null) return Err("no camera in scene (open one or enter Play)");

        var rt = new RenderTexture(w, h, 24);
        var prev = cam.targetTexture;
        var prevActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return Ok(path);
        }
        finally
        {
            cam.targetTexture = prev;
            RenderTexture.active = prevActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    private static List<Component> UGuiButtons()
    {
        var list = new List<Component>();
        Type t = ResolveType("UnityEngine.UI.Button");
        if (t == null) return list;
        foreach (var o in Resources.FindObjectsOfTypeAll(t))
        {
            var c = o as Component;
            if (c != null && c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy) list.Add(c);
        }
        return list;
    }
    private static bool BtnInteractable(Component b)
    {
        try { var p = b.GetType().GetProperty("interactable", BindingFlags.Public | BindingFlags.Instance); return p == null || !(p.GetValue(b) is bool bo) || bo; }
        catch { return true; }
    }
    private static string BtnInvoke(Component b)
    {
        try
        {
            var oc = b.GetType().GetProperty("onClick", BindingFlags.Public | BindingFlags.Instance)?.GetValue(b);
            var inv = oc?.GetType().GetMethod("Invoke", Type.EmptyTypes);
            if (inv == null) return "no onClick.Invoke()";
            inv.Invoke(oc, null);
            return null;
        }
        catch (Exception e) { return e.InnerException?.Message ?? e.Message; }
    }

    private static string UiClick(string text, string scope)
    {
        if (string.IsNullOrEmpty(text)) return Err("missing text");
        var visible = new List<(string label, string path, bool interactable)>();
        var matches = new List<(Component btn, string label, string path)>();
        foreach (var b in UGuiButtons())
        {
            string path = GetGoPath(b.gameObject);
            string label = ButtonLabel(b);
            bool inter = BtnInteractable(b);
            visible.Add((label ?? "<no-label>", path, inter));
            if (string.IsNullOrEmpty(label)) continue;
            if (label.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!string.IsNullOrEmpty(scope) && path.IndexOf(scope, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!inter) continue;
            matches.Add((b, label, path));
        }
        if (matches.Count == 0)
        {

            var tkHits = new List<VisualElement>();
            foreach (var ve in AllVisualElements())
            {
                if (!VeClickable(ve)) continue;
                string label2 = VeText(ve);
                if (string.IsNullOrEmpty(label2) || label2.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!string.IsNullOrEmpty(scope) && VePath(ve).IndexOf(scope, StringComparison.OrdinalIgnoreCase) < 0) continue;
                tkHits.Add(ve);
            }
            if (tkHits.Count == 1)
            {
                if (ClickVe(tkHits[0], out string tkErr)) return Ok("clicked (UITK) \"" + (VeText(tkHits[0]) ?? "") + "\" @ " + VePath(tkHits[0]));
                return Err("UITK click failed: " + tkErr);
            }
            if (tkHits.Count > 1)
            {
                var amb = new StringBuilder("ambiguous (UITK): ").Append(tkHits.Count).Append(" elements match \"").Append(text).Append("\":");
                foreach (var ve in tkHits) amb.Append("\n  ").Append(VeText(ve)).Append("  @ ").Append(VePath(ve));
                return Err(amb.ToString());
            }
            var sb = new StringBuilder("no interactable Button matches text=\"").Append(text).Append('"');
            if (!string.IsNullOrEmpty(scope)) sb.Append(" scope=\"").Append(scope).Append('"');
            sb.Append(". Visible buttons (").Append(visible.Count).Append("):");
            foreach (var v in visible.Take(40))
                sb.Append("\n  ").Append(v.interactable ? "  " : "✗ ").Append(v.label).Append("  @ ").Append(v.path);
            if (visible.Count > 40) sb.Append("\n  ... (").Append(visible.Count - 40).Append(" more)");
            return Err(sb.ToString());
        }
        if (matches.Count > 1)
        {
            var sb = new StringBuilder("ambiguous: ").Append(matches.Count).Append(" Button matches for \"").Append(text).Append("\":");
            foreach (var m in matches) sb.Append("\n  ").Append(m.label).Append("  @ ").Append(m.path);
            sb.Append("\nDisambiguate: scope=<path-substring>");
            return Err(sb.ToString());
        }
        var hit = matches[0];
        string cerr = BtnInvoke(hit.btn);
        if (cerr != null) return Err("onClick threw: " + cerr + "\n  on: " + hit.path);
        return Ok("clicked \"" + hit.label + "\" @ " + hit.path);
    }

    private static string UiList(string scope)
    {
        var sb = new StringBuilder("{\"ok\":true,\"result\":[");
        bool first = true;
        foreach (var b in UGuiButtons())
        {
            string path = GetGoPath(b.gameObject);
            if (!string.IsNullOrEmpty(scope) && path.IndexOf(scope, StringComparison.OrdinalIgnoreCase) < 0) continue;
            string label = ButtonLabel(b) ?? "";
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"label\":").Append(JsonString(label))
              .Append(",\"path\":").Append(JsonString(path))
              .Append(",\"interactable\":").Append(BtnInteractable(b) ? "true" : "false")
              .Append('}');
        }

        foreach (var ve in AllVisualElements())
        {
            if (!VeClickable(ve)) continue;
            string tkPath = VePath(ve);
            if (!string.IsNullOrEmpty(scope) && tkPath.IndexOf(scope, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"label\":").Append(JsonString(VeText(ve) ?? ""))
              .Append(",\"path\":").Append(JsonString(tkPath))
              .Append(",\"interactable\":").Append(ve.enabledInHierarchy ? "true" : "false")
              .Append(",\"tk\":true}");
        }
        return sb.Append("]}").ToString();
    }

    private static string ButtonLabel(Component b)
    {
        foreach (var c in b.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            var t = c.GetType();
            var nameLower = t.Name.ToLowerInvariant();
            if (!nameLower.Contains("text") && !nameLower.Contains("label")) continue;
            var prop = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(string)) continue;
            try
            {
                var val = prop.GetValue(c) as string;
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
            catch { }
        }
        return null;
    }

    private static string GetGoPath(GameObject go)
    {
        var sb = new StringBuilder(go.name);
        for (var t = go.transform.parent; t != null; t = t.parent)
            sb.Insert(0, t.name + "/");
        return sb.ToString();
    }

    private static Type ResolveType(string fullName)
    {
        var direct = Type.GetType(fullName);
        if (direct != null) return direct;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(fullName, false);
            if (t != null) return t;
        }
        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null); }
        catch { return Array.Empty<Type>(); }
    }

    private static bool ExecAllowed() => Environment.GetEnvironmentVariable("DCL_IPC_ALLOW_EXEC") != "0";

    private static string Capabilities()
    {
        string[] ops = {
            "ping","capabilities","exec","compile","scene-roots","hierarchy","find-type","asmdefs",
            "game-rect","screenshot-game","screenshot","ui-click","ui-list","component-get",
            "component-set","text","world-ready","log"
        };
        var sb = new StringBuilder("{\"ok\":true,\"result\":{\"version\":\"2\",\"editor\":");
        sb.Append(JsonString(Application.unityVersion));
        sb.Append(",\"playing\":").Append(EditorApplication.isPlaying ? "true" : "false");
        sb.Append(",\"execEnabled\":").Append(ExecAllowed() ? "true" : "false");
        sb.Append(",\"ops\":[");
        for (int i = 0; i < ops.Length; i++) { if (i > 0) sb.Append(','); sb.Append(JsonString(ops[i])); }
        return sb.Append("]}}").ToString();
    }

    private static List<VisualElement> AllVisualElements()
    {
        var all = new List<VisualElement>();
        foreach (var doc in Resources.FindObjectsOfTypeAll<UIDocument>())
        {
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) continue;
            all.AddRange(root.Query<VisualElement>().ToList());
        }
        return all;
    }

    private static bool HasClickable(VisualElement ve)
    {
        var f = ve.GetType().GetField("m_Clickable", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null && f.GetValue(ve) != null;
    }

    private static bool VeClickable(VisualElement ve) =>
        ve is Button || ve is Toggle || HasClickable(ve);

    private static string VeText(VisualElement ve)
    {
        switch (ve)
        {
            case Button b:      return b.text;
            case Label l:       return l.text;
            case TextElement t: return t.text;
        }
        var p = ve.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.PropertyType == typeof(string)) { try { return p.GetValue(ve) as string; } catch { } }
        return null;
    }

    private static string VePath(VisualElement ve)
    {
        var sb = new StringBuilder(string.IsNullOrEmpty(ve.name) ? ve.GetType().Name : ve.name);
        for (var p = ve.parent; p != null; p = p.parent)
            sb.Insert(0, (string.IsNullOrEmpty(p.name) ? p.GetType().Name : p.name) + "/");
        return sb.ToString();
    }

    private static bool ClickVe(VisualElement ve, out string err)
    {
        err = null;
        try
        {
            var clk = ve.GetType().GetField("m_Clickable", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ve);
            if (clk != null)
            {
                var del = clk.GetType().GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(clk) as Delegate;
                if (del != null) { del.DynamicInvoke(); return true; }
            }
            if (ve is Toggle tg) { tg.value = !tg.value; return true; }
            err = "no Clickable on " + ve.GetType().Name;
            return false;
        }
        catch (Exception e) { err = e.InnerException?.Message ?? e.Message; return false; }
    }

    private static GameObject FindGo(string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath)) return null;
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go.name == nameOrPath || GetGoPath(go) == nameOrPath || GetGoPath(go).EndsWith("/" + nameOrPath, StringComparison.Ordinal))
                return go;
        return null;
    }

    private static Component ResolveComponent(GameObject go, string compName, out string err)
    {
        err = null;
        if (string.IsNullOrEmpty(compName)) { err = "specify component=<TypeName>"; return null; }
        foreach (var c in go.GetComponents<Component>())
            if (c != null && (c.GetType().Name == compName || c.GetType().FullName == compName)) return c;
        err = "component not found on " + go.name + ": " + compName;
        return null;
    }

    private static object GetMemberValue(object o, string member, out string err)
    {
        err = null;
        var t = o.GetType();
        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanRead) { try { return p.GetValue(o); } catch (Exception e) { err = "get threw: " + (e.InnerException?.Message ?? e.Message); return null; } }
        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(o);
        err = "public instance property/field not found: " + member;
        return null;
    }

    private static string SetMemberValue(object o, string member, string raw)
    {
        var t = o.GetType();
        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite)
        {
            try { p.SetValue(o, ParseArg(raw, p.PropertyType)); return null; }
            catch (Exception e) { return "set threw: " + (e.InnerException?.Message ?? e.Message); }
        }
        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null)
        {
            try { f.SetValue(o, ParseArg(raw, f.FieldType)); return null; }
            catch (Exception e) { return "set threw: " + (e.InnerException?.Message ?? e.Message); }
        }
        return "writable public member not found: " + member;
    }

    private static string ComponentGet(string objName, string compName, string member)
    {
        if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(member)) return Err("component-get needs object=<name|path> and member=<name>");
        var go = FindGo(objName);
        if (go == null) return Err("GameObject not found: " + objName);
        var comp = ResolveComponent(go, compName, out string cerr);
        if (comp == null) return Err(cerr);
        var val = GetMemberValue(comp, member, out string merr);
        if (merr != null) return Err(merr);
        return "{\"ok\":true,\"result\":" + JsonValue(val) + "}";
    }

    private static string ComponentSet(string objName, string compName, string member, string value)
    {
        if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(member)) return Err("component-set needs object, member, value");
        var go = FindGo(objName);
        if (go == null) return Err("GameObject not found: " + objName);
        var comp = ResolveComponent(go, compName, out string cerr);
        if (comp == null) return Err(cerr);
        string serr = SetMemberValue(comp, member, value);
        if (serr != null) return Err(serr);
        return Ok("set " + compName + "." + member + " = " + value + " on " + objName);
    }

    private static string TextInput(string target, string value)
    {
        if (string.IsNullOrEmpty(target)) return Err("text needs target=<name>");
        value ??= "";
        foreach (var ve in AllVisualElements())
            if (ve is TextField tf && (ve.name == target || VePath(ve).EndsWith("/" + target, StringComparison.Ordinal)))
            { tf.value = value; return Ok("set UITK TextField '" + target + "'"); }

        var go = FindGo(target);
        if (go != null)
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || !c.GetType().Name.Contains("InputField")) continue;
                var prop = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite) { prop.SetValue(c, value); return Ok("set " + c.GetType().Name + ".text on " + target); }
            }
        return Err("no UITK TextField / uGUI InputField named " + target);
    }

    private static string Hierarchy(string scope, int maxDepth, int maxNodes)
    {
        var sb = new StringBuilder("{\"ok\":true,\"result\":{\"gameObjects\":[");
        int count = 0; bool first = true;
        void Walk(Transform tr, int depth)
        {
            if (count >= maxNodes || depth > maxDepth) return;
            var go = tr.gameObject;
            string path = GetGoPath(go);
            if (string.IsNullOrEmpty(scope) || path.IndexOf(scope, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!first) sb.Append(',');
                first = false; count++;
                sb.Append("{\"name\":").Append(JsonString(go.name))
                  .Append(",\"depth\":").Append(depth)
                  .Append(",\"active\":").Append(go.activeInHierarchy ? "true" : "false")
                  .Append(",\"path\":").Append(JsonString(path)).Append('}');
            }
            for (int i = 0; i < tr.childCount && count < maxNodes; i++) Walk(tr.GetChild(i), depth + 1);
        }
        for (int s = 0; s < SceneManager.sceneCount && count < maxNodes; s++)
            foreach (var root in SceneManager.GetSceneAt(s).GetRootGameObjects())
                Walk(root.transform, 0);

        sb.Append("],\"uitk\":[");
        bool firstVe = true; int veCount = 0;
        foreach (var ve in AllVisualElements())
        {
            if (veCount >= maxNodes) break;
            string path = VePath(ve);
            if (!string.IsNullOrEmpty(scope) && path.IndexOf(scope, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!firstVe) sb.Append(',');
            firstVe = false; veCount++;
            sb.Append("{\"type\":").Append(JsonString(ve.GetType().Name))
              .Append(",\"name\":").Append(JsonString(ve.name ?? ""))
              .Append(",\"text\":").Append(JsonString(VeText(ve) ?? ""))
              .Append(",\"clickable\":").Append(VeClickable(ve) ? "true" : "false")
              .Append(",\"path\":").Append(JsonString(path)).Append('}');
        }
        return sb.Append("]}}").ToString();
    }

    private static string ScreenshotComposited(string path, int superSize)
    {
        if (string.IsNullOrEmpty(path)) return Err("missing path");
        Texture2D tex = null, fixedTex = null;
        try
        {
            tex = ScreenCapture.CaptureScreenshotAsTexture(Mathf.Clamp(superSize, 1, 4));
            fixedTex = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
            fixedTex.SetPixels(tex.GetPixels()); fixedTex.Apply();
            File.WriteAllBytes(path, fixedTex.EncodeToPNG());
            return Ok(path);
        }
        catch (Exception e) { return Err("composited capture failed: " + e.Message); }
        finally
        {
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            if (fixedTex != null) UnityEngine.Object.DestroyImmediate(fixedTex);
        }
    }

    private static string JsonValue(object v)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (v == null) return "null";
        if (v is bool b) return b ? "true" : "false";
        if (v is string s) return JsonString(s);
        if (v is float f) return f.ToString("R", inv);
        if (v is double d) return d.ToString("R", inv);
        if (v is decimal m) return m.ToString(inv);
        if (v is Enum) return JsonString(v.ToString());
        if (v is sbyte || v is byte || v is short || v is ushort || v is int || v is uint || v is long || v is ulong)
            return Convert.ToString(v, inv);
        return JsonString(v.ToString());
    }

    private static Dictionary<string, string> ParseFlat(string s)
    {
        var d = new Dictionary<string, string>();
        if (s == null) return d;
        int i = s.IndexOf('{'); if (i < 0) return d;
        int end = s.LastIndexOf('}'); if (end <= i) return d;
        s = s.Substring(i + 1, end - i - 1);
        int p = 0;
        while (p < s.Length)
        {
            while (p < s.Length && (char.IsWhiteSpace(s[p]) || s[p] == ',')) p++;
            if (p >= s.Length || s[p] != '"') break;
            int kEnd = s.IndexOf('"', p + 1);
            if (kEnd < 0) break;
            string k = s.Substring(p + 1, kEnd - p - 1);
            int colon = s.IndexOf(':', kEnd); if (colon < 0) break;
            p = colon + 1;
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
            string v;
            if (s[p] == '"')
            {
                int vEnd = p + 1;
                while (vEnd < s.Length && !(s[vEnd] == '"' && s[vEnd - 1] != '\\')) vEnd++;
                v = s.Substring(p + 1, vEnd - p - 1).Replace("\\\"", "\"").Replace("\\\\", "\\");
                p = vEnd + 1;
            }
            else
            {
                int vEnd = p;
                while (vEnd < s.Length && s[vEnd] != ',' && s[vEnd] != '}' && !char.IsWhiteSpace(s[vEnd])) vEnd++;
                v = s.Substring(p, vEnd - p);
                p = vEnd;
            }
            d[k] = v;
        }
        return d;
    }

    private static string Get(Dictionary<string, string> c, string k) =>
        c.TryGetValue(k, out var v) ? v : null;

    private static int IntOr(Dictionary<string, string> c, string k, int dflt) =>
        c.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : dflt;

    private static string Ok(object result) =>
        "{\"ok\":true,\"result\":" + JsonValue(result) + "}";

    private static string Err(string msg) =>
        "{\"ok\":false,\"error\":" + JsonString(msg) + "}";

    private static string JsonString(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20) sb.AppendFormat("\\u{0:x4}", (int)ch);
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
#endif
