namespace OpenTui.Core;

/// <summary>
/// Options for creating a Timeline.
/// Matches TypeScript TimelineOptions.
/// </summary>
public class TimelineOptions
{
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public float Duration { get; init; } = 1000;
    /// <summary>
    /// Gets or sets the loop.
    /// </summary>
    public bool Loop { get; init; }
    /// <summary>
    /// Gets or sets the auto play.
    /// </summary>
    public bool AutoPlay { get; init; } = true;
    /// <summary>
    /// Gets or sets the on complete.
    /// </summary>
    public Action? OnComplete { get; init; }
    /// <summary>
    /// Gets or sets the on pause.
    /// </summary>
    public Action? OnPause { get; init; }
}

/// <summary>
/// Options for an individual animation within a Timeline.
/// Matches TypeScript AnimationOptions.
///
/// Since C# can't mutate arbitrary object properties by name (AOT-safe),
/// target property animations are specified via TweenProperty objects
/// that contain getter/setter delegates.
/// </summary>
public class AnimationOptions
{
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public float Duration { get; init; } = 1000;
    /// <summary>
    /// Gets or sets the ease.
    /// </summary>
    public string Ease { get; init; } = "linear";
    /// <summary>
    /// Gets or sets the loop.
    /// </summary>
    public bool Loop { get; init; }
    /// <summary>
    /// Gets or sets the loop count.
    /// </summary>
    public int LoopCount { get; init; } = 1;
    /// <summary>
    /// Gets or sets the loop delay.
    /// </summary>
    public float LoopDelay { get; init; }
    /// <summary>
    /// Gets or sets the alternate.
    /// </summary>
    public bool Alternate { get; init; }
    /// <summary>
    /// Gets or sets the once.
    /// </summary>
    public bool Once { get; init; }
    /// <summary>
    /// Gets or sets the on update.
    /// </summary>
    public Action<AnimationState>? OnUpdate { get; init; }
    /// <summary>
    /// Gets or sets the on complete.
    /// </summary>
    public Action? OnComplete { get; init; }
    /// <summary>
    /// Gets or sets the on start.
    /// </summary>
    public Action? OnStart { get; init; }
    /// <summary>
    /// Gets or sets the on loop.
    /// </summary>
    public Action? OnLoop { get; init; }

    /// <summary>
    /// The properties to tween. Each entry defines a start→end interpolation
    /// with getter/setter for the target property.
    /// </summary>
    public List<TweenProperty> Properties { get; init; } = [];
}

/// <summary>
/// Defines a single tweened property with get/set delegates.
/// </summary>
public class TweenProperty
{
    /// <summary>Gets the current value from the target.</summary>
    public required Func<float> Get { get; init; }

    /// <summary>Sets the interpolated value on the target.</summary>
    public required Action<float> Set { get; init; }

    /// <summary>The end value to interpolate toward.</summary>
    public required float EndValue { get; init; }

    /// <summary>Captured initial value (set lazily on first tick).</summary>
    internal float StartValue { get; set; }

    /// <summary>Whether the initial value has been captured.</summary>
    internal bool Captured { get; set; }
}

/// <summary>
/// State passed to animation update callbacks.
/// Matches TypeScript JSAnimation.
/// </summary>
public readonly record struct AnimationState(
    float DeltaTime,
    float Progress,
    float CurrentTime);

/// <summary>
/// Keyframe animation timeline with easing, looping, and nesting.
/// Matches TypeScript Timeline from Timeline.ts.
///
/// Usage:
///   var tl = new Timeline(new() { Duration = 2000 });
///   tl.Add(new AnimationOptions {
///       Duration = 1000,
///       Ease = "outQuad",
///       Properties = [new() { Get = () => box.Opacity, Set = v => box.Opacity = v, EndValue = 0 }]
///   });
///   // In render loop: tl.Update(deltaTimeMs);
/// </summary>
public class Timeline
{
    private readonly List<AnimationItem> _items = [];
    private readonly List<CallbackItem> _callbacks = [];
    private readonly List<SubTimelineItem> _subTimelines = [];
    private readonly List<Action<Timeline>> _stateListeners = [];
    private readonly Func<float, float> _defaultEase;

    private float _duration;
    private bool _loop;
    private bool _autoPlay;
    private Action? _onComplete;
    private Action? _onPause;

    /// <summary>
    /// Gets or sets the current time.
    /// </summary>
    public float CurrentTime { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether is playing.
    /// </summary>
    public bool IsPlaying { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether is complete.
    /// </summary>
    public bool IsComplete { get; private set; }
    /// <summary>
    /// Gets or sets the synced.
    /// </summary>
    public bool Synced { get; internal set; }

    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public float Duration
    {
        get => _duration;
        set => _duration = value;
    }

    /// <summary>
    /// Gets or sets the loop.
    /// </summary>
    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    /// <summary>
    /// Initializes a new instance of the Timeline class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public Timeline(TimelineOptions? options = null)
    {
        options ??= new TimelineOptions();
        _duration = options.Duration;
        _loop = options.Loop;
        _autoPlay = options.AutoPlay;
        _onComplete = options.OnComplete;
        _onPause = options.OnPause;
        _defaultEase = Easing.Linear;

        if (_autoPlay)
            Play();
    }

    #region Add Animations

    /// <summary>
    /// Adds an animation starting at the given time offset.
    /// </summary>
    public Timeline Add(AnimationOptions anim, float startTime = 0)
    {
        var ease = Easing.ByName(anim.Ease);
        int maxLoops = anim.Loop ? int.MaxValue
            : anim.LoopCount > 1 ? anim.LoopCount : 1;

        _items.Add(new AnimationItem
        {
            StartTime = startTime,
            Duration = anim.Duration,
            EasingFn = ease,
            MaxLoops = maxLoops,
            LoopDelay = anim.LoopDelay,
            Alternate = anim.Alternate,
            Once = anim.Once,
            Properties = anim.Properties,
            OnUpdate = anim.OnUpdate,
            OnComplete = anim.OnComplete,
            OnStart = anim.OnStart,
            OnLoop = anim.OnLoop,
        });

        return this;
    }

    /// <summary>
    /// Adds a fire-and-forget animation at the current time.
    /// </summary>
    public Timeline Once(AnimationOptions anim)
    {
        anim = new AnimationOptions
        {
            Duration = anim.Duration,
            Ease = anim.Ease,
            Properties = anim.Properties,
            OnUpdate = anim.OnUpdate,
            OnComplete = anim.OnComplete,
            OnStart = anim.OnStart,
            Once = true,
        };
        return Add(anim, CurrentTime);
    }

    /// <summary>
    /// Schedules a callback at the given time.
    /// </summary>
    public Timeline Call(Action callback, float startTime = 0)
    {
        _callbacks.Add(new CallbackItem { StartTime = startTime, Callback = callback });
        return this;
    }

    /// <summary>
    /// Nests a child timeline that is driven by this parent.
    /// </summary>
    public Timeline Sync(Timeline child, float startTime = 0)
    {
        if (child.Synced)
            throw new InvalidOperationException("Timeline is already synced to another parent.");
        child.Synced = true;
        _subTimelines.Add(new SubTimelineItem { StartTime = startTime, Child = child });
        return this;
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// Performs play.
    /// </summary>
    /// <returns>The result of play.</returns>
    public Timeline Play()
    {
        if (IsComplete) Restart();
        IsPlaying = true;
        foreach (var sub in _subTimelines)
            sub.Child.Play();
        NotifyStateChange();
        return this;
    }

    /// <summary>
    /// Performs pause.
    /// </summary>
    /// <returns>The result of pause.</returns>
    public Timeline Pause()
    {
        IsPlaying = false;
        _onPause?.Invoke();
        foreach (var sub in _subTimelines)
            sub.Child.Pause();
        NotifyStateChange();
        return this;
    }

    /// <summary>
    /// Performs restart.
    /// </summary>
    /// <returns>The result of restart.</returns>
    public Timeline Restart()
    {
        CurrentTime = 0;
        IsComplete = false;
        ResetItems();
        NotifyStateChange();
        return this;
    }

    /// <summary>
    /// Performs reset items.
    /// </summary>
    public void ResetItems()
    {
        foreach (var item in _items)
        {
            item.Completed = false;
            item.Started = false;
            item.CurrentLoop = 0;
            foreach (var prop in item.Properties)
                prop.Captured = false;
        }
        foreach (var cb in _callbacks)
            cb.Executed = false;
        foreach (var sub in _subTimelines)
        {
            sub.TimelineStarted = false;
            sub.Child.Restart();
        }
    }

    #endregion

    #region Update (Tick)

    /// <summary>
    /// Advances the timeline by deltaTime milliseconds.
    /// Called from the render loop or TimelineEngine.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!IsPlaying || IsComplete) return;

        CurrentTime += deltaTime;

        // Process animations
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (item.Completed) continue;
            if (CurrentTime < item.StartTime) continue;

            float animTime = CurrentTime - item.StartTime;

            // Lazy capture initial values
            if (!item.Started)
            {
                item.Started = true;
                foreach (var prop in item.Properties)
                {
                    if (!prop.Captured)
                    {
                        prop.StartValue = prop.Get();
                        prop.Captured = true;
                    }
                }
                item.OnStart?.Invoke();
            }

            // Calculate loop progress
            float cycleTime = item.Duration + item.LoopDelay;
            int currentCycle = cycleTime > 0 ? (int)(animTime / cycleTime) : 0;

            if (currentCycle >= item.MaxLoops)
            {
                // Complete
                ApplyProgress(item, 1f);
                item.Completed = true;
                item.OnComplete?.Invoke();
                if (item.Once)
                    _items.RemoveAt(i);
                continue;
            }

            // Fire loop callback at cycle boundaries
            if (currentCycle > item.CurrentLoop)
            {
                item.OnLoop?.Invoke();
                item.CurrentLoop = currentCycle;
            }

            float cycleProgress = cycleTime > 0
                ? (animTime - currentCycle * cycleTime) / item.Duration
                : animTime / item.Duration;
            cycleProgress = Math.Clamp(cycleProgress, 0f, 1f);

            // Alternate direction on odd cycles
            if (item.Alternate && currentCycle % 2 == 1)
                cycleProgress = 1f - cycleProgress;

            float easedProgress = item.EasingFn(cycleProgress);
            ApplyProgress(item, easedProgress);

            item.OnUpdate?.Invoke(new AnimationState(deltaTime, easedProgress, CurrentTime));
        }

        // Process callbacks
        foreach (var cb in _callbacks)
        {
            if (!cb.Executed && CurrentTime >= cb.StartTime)
            {
                cb.Executed = true;
                cb.Callback();
            }
        }

        // Process sub-timelines
        foreach (var sub in _subTimelines)
        {
            if (CurrentTime >= sub.StartTime)
            {
                if (!sub.TimelineStarted)
                {
                    sub.TimelineStarted = true;
                    sub.Child.Play();
                }
                sub.Child.Update(deltaTime);
            }
        }

        // Check completion
        if (CurrentTime >= _duration)
        {
            if (_loop)
            {
                CurrentTime = 0;
                ResetItems();
            }
            else
            {
                IsComplete = true;
                IsPlaying = false;
                _onComplete?.Invoke();
                NotifyStateChange();
            }
        }
    }

    private static void ApplyProgress(AnimationItem item, float easedProgress)
    {
        foreach (var prop in item.Properties)
        {
            float value = prop.StartValue + (prop.EndValue - prop.StartValue) * easedProgress;
            prop.Set(value);
        }
    }

    #endregion

    #region State Listeners

    /// <summary>
    /// Adds a state change listener.
    /// </summary>
    /// <param name="listener">The listener.</param>
    public void AddStateChangeListener(Action<Timeline> listener) =>
        _stateListeners.Add(listener);

    /// <summary>
    /// Removes a state change listener.
    /// </summary>
    /// <param name="listener">The listener.</param>
    public void RemoveStateChangeListener(Action<Timeline> listener) =>
        _stateListeners.Remove(listener);

    private void NotifyStateChange()
    {
        foreach (var listener in _stateListeners)
            listener(this);
    }

    #endregion

    #region Internal Types

    private sealed class AnimationItem
    {
        public float StartTime;
        public float Duration;
        public Func<float, float> EasingFn = Easing.Linear;
        public int MaxLoops = 1;
        public float LoopDelay;
        public bool Alternate;
        public bool Once;
        public List<TweenProperty> Properties = [];
        public Action<AnimationState>? OnUpdate;
        public Action? OnComplete;
        public Action? OnStart;
        public Action? OnLoop;
        public bool Completed;
        public bool Started;
        public int CurrentLoop;
    }

    private sealed class CallbackItem
    {
        public float StartTime;
        public Action Callback = null!;
        public bool Executed;
    }

    private sealed class SubTimelineItem
    {
        public float StartTime;
        public Timeline Child = null!;
        public bool TimelineStarted;
    }

    #endregion
}
