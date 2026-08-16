#nullable disable
using System;
using UnityEngine;
using Moq;

public interface Widget { }

public interface IWidget { }

public class Violations
{
    private readonly ObjectProxy<int> proxy = new ObjectProxy<int>();

    [ContextMenu("run")]
    public void Run()
    {
        Debug.Log("plain");
        UnityEngine.Debug.LogWarning("qualified");
        Debug.Assert(true);
        MyDebug.Log("negative");
        var cam = Camera.main;
        flags.IsEnabled("explorer-flag");
        flags.IsEnabled("flag");
        builder.TryAddWidget("w").Add();
        builder.TryAddWidget("w")?.Add();
        ReportHub.LogError("Literal", "msg");
        ReportHub.LogError(ReportCategory.ENGINE, "msg");
        world.Query(in desc, Fn);
        var http = new HttpClient();
        throw new ArgumentNullException("param");
    }
}
// ReSharper disable once CheckNamespace
class Suppressed { void F() { Debug.Log("s"); } } // lint-ignore: debug-log
class WrongId { void F() { var c = Camera.main; } } // lint-ignore: debug-log

class Builders
{
    void F(System.Text.StringBuilder b, int n)
    {
        b.Append($"count={n}");
        b.AppendLine($"row {n}");
        b.Append("literal is fine").Append(n);
        int cached = n; // so the caller can retry with the same value
        int local = n;  // remove the corrupt file so the next read doesn't hit it
    }
}

class NullableStates
{
    void F()
    {
        EventId? maybeEvent = events[i];
        Dictionary<string, int>? lookup = TryGetLookup();
        EventId forced = events[i]!;
        Process(target!);
        Target found = registry.Find(id)!.Target;
        viewInstance!.Show();
        DCLInput.Instance!.Shortcuts.Register();
        MoveQuery(World!);
        var copied = world.Get<Movement>(entity);
        ref var safe = ref world.Get<Movement>(entity);
        var unrelated = worldService.GetAll();
        EventId fine = events[i];
        string dtoInit = null!;
        int score = has ? a : b;
    }

    void G(
        DebugController? debugController = null,
        Button? debugButton = null)
    { }
}

class MinedRules
{
    private readonly ConcurrentDictionary<string, int> cache = new ConcurrentDictionary<string, int>();
    private readonly ConcurrentQueue<int> pending = new ConcurrentQueue<int>();
    private readonly DCLConcurrentDictionary<string, int> sanctioned = new DCLConcurrentDictionary<string, int>();
    private readonly DCLConcurrentQueue<int> sanctionedQueue = new DCLConcurrentQueue<int>();

    void F(string name, string other)
    {
        var url = "https://peer.decentraland.org/content/entities";
        var root = "https://decentraland.org/marketplace/names/claim";
        var external = "https://example.com/status";
        // see https://docs.decentraland.org/creator/scenes-sdk7/ for the cap
        if (name.ToLowerInvariant() == other.ToLowerInvariant()) return;
        if (name.ToLower().StartsWith("wss://")) return;
        if (name.Equals(other.ToLowerInvariant())) return;
        if (string.Equals(name, other, StringComparison.OrdinalIgnoreCase)) return;
        var dir = Application.persistentDataPath + "/AvatarCache";
        var joined = Application.persistentDataPath + FOLDER_NAME;
        var initJs = $"file://{Application.streamingAssetsPath}/Js/Init.js";
        var combined = Path.Combine(Application.persistentDataPath, "AvatarCache");
        try { F(name, other); }
        catch (Exception) { }
        try { F(name, other); }
        catch { }
        try { F(name, other); }
        catch (OperationCanceledException) { }
        try { F(name, other); }
        catch (ObjectDisposedException) { }
        // Note that this ensures the cache is always warm.
        // remove the corrupt entry so the next read rebuilds it
    }
}
