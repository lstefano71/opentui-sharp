namespace OpenTui.Core;

/// <summary>
/// Singleton engine that bridges timelines to the renderer's frame callback loop.
/// Matches TypeScript TimelineEngine from Timeline.ts.
/// </summary>
public sealed class TimelineEngine
{
    /// <summary>
    /// Stores the instance.
    /// </summary>
    public static readonly TimelineEngine Instance = new();

    private readonly List<Timeline> _timelines = [];
    private CliRenderer? _renderer;
    private Func<float, Task>? _frameCallback;
    private bool _attached;
    private int _liveCount;

    private TimelineEngine() { }

    /// <summary>
    /// Attaches the engine to a renderer's frame callback.
    /// </summary>
    public void Attach(CliRenderer renderer)
    {
        if (_attached) Detach();
        _renderer = renderer;
        _frameCallback = dt => { Update(dt); return Task.CompletedTask; };
        _renderer.AddFrameCallback(_frameCallback);
        _attached = true;
    }

    /// <summary>
    /// Detaches from the renderer and clears all timelines.
    /// </summary>
    public void Detach()
    {
        if (_renderer is not null && _frameCallback is not null)
            _renderer.RemoveFrameCallback(_frameCallback);
        _renderer = null;
        _frameCallback = null;
        _attached = false;
        _liveCount = 0;
        _timelines.Clear();
    }

    /// <summary>
    /// Registers a timeline with the engine.
    /// </summary>
    public void Register(Timeline timeline)
    {
        if (_timelines.Contains(timeline)) return;
        _timelines.Add(timeline);
        timeline.AddStateChangeListener(OnTimelineStateChange);
        if (timeline.IsPlaying) RequestLive();
    }

    /// <summary>
    /// Unregisters a timeline from the engine.
    /// </summary>
    public void Unregister(Timeline timeline)
    {
        if (!_timelines.Remove(timeline)) return;
        timeline.RemoveStateChangeListener(OnTimelineStateChange);
        if (timeline.IsPlaying) DropLive();
    }

    /// <summary>
    /// Removes all timelines.
    /// </summary>
    public void Clear()
    {
        foreach (var tl in _timelines)
            tl.RemoveStateChangeListener(OnTimelineStateChange);
        _timelines.Clear();
        _liveCount = 0;
    }

    /// <summary>
    /// Ticks all non-synced timelines. Called by the renderer frame callback.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Iterate a snapshot to allow modification during update
        var count = _timelines.Count;
        for (int i = 0; i < count && i < _timelines.Count; i++)
        {
            var tl = _timelines[i];
            if (!tl.Synced)
                tl.Update(deltaTime);
        }
    }

    private void OnTimelineStateChange(Timeline timeline)
    {
        if (timeline.IsPlaying)
            RequestLive();
        else
            DropLive();
    }

    private void RequestLive()
    {
        _liveCount++;
        _renderer?.RequestLive();
    }

    private void DropLive()
    {
        if (_liveCount > 0) _liveCount--;
        _renderer?.DropLive();
    }
}

/// <summary>
/// Factory for creating and auto-registering timelines.
/// </summary>
public static class TimelineFactory
{
    /// <summary>
    /// Creates a timeline and registers it with the singleton engine.
    /// Matches TypeScript createTimeline().
    /// </summary>
    public static Timeline CreateTimeline(TimelineOptions? options = null)
    {
        var timeline = new Timeline(options);
        TimelineEngine.Instance.Register(timeline);
        return timeline;
    }
}
