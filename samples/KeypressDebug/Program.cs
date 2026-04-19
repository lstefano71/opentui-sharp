using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 60,
    UseKittyKeyboard = new KittyKeyboardOptions
    {
        AlternateKeys = true,
        Disambiguate = true,
        Events = true,
    },
});

renderer.SetBackgroundColor(Rgba.FromHex("#0D1117"));
renderer.SetDebugMode(true);

bool showJson = string.Equals(
    Environment.GetEnvironmentVariable("OTUI_KEYPRESS_DEBUG_SHOW_JSON"),
    "true",
    StringComparison.OrdinalIgnoreCase);
bool showingHelp = false;
int eventCount = 0;
string? lastSavedFile = null;
string? lastSaveError = null;

List<RawInputRecord> allRawInputs = renderer.GetDebugInputs()
    .Select(record => new RawInputRecord { Timestamp = record.Timestamp, Sequence = record.Sequence })
    .ToList();
List<SavedEventRecord> allKeyEvents = [];
List<DebugEntry> visibleEvents = [];
RawInputRecord? lastRawInput = allRawInputs.LastOrDefault();

var mainContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "keypress-debug-main",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Percent(100),
    Padding = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Column,
});

var statusText = new TextRenderable(renderer, new TextOptions
{
    Id = "keypress-debug-status",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(2),
    FlexShrink = 0,
    Fg = Rgba.FromHex("#E6EDF3"),
    WrapMode = WrapMode.Word,
    Selectable = false,
});

var body = new BoxRenderable(renderer, new BoxOptions
{
    Id = "keypress-debug-body",
    Width = DimensionValue.Percent(100),
    FlexGrow = 1,
    FlexShrink = 1,
    FlexDirection = FlexDirectionValue.Row,
});

var eventFeed = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "keypress-debug-feed",
    FlexGrow = 1,
    FlexShrink = 1,
    Border = true,
    BorderColor = Rgba.FromHex("#58A6FF"),
    Title = "Event Feed",
    BackgroundColor = Rgba.FromHex("#0F1722"),
    StickyScroll = true,
    StickyStart = "bottom",
    ContentOptions = new BoxOptions
    {
        Padding = DimensionValue.Point(1),
    },
});

var eventListText = new TextRenderable(renderer, new TextOptions
{
    Id = "keypress-debug-feed-text",
    Width = DimensionValue.Percent(100),
    WrapMode = WrapMode.None,
    Truncate = true,
    Fg = Rgba.FromHex("#C9D1D9"),
    Selectable = false,
});
eventFeed.Add(eventListText);

var detailFeed = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "keypress-debug-detail-feed",
    Width = DimensionValue.Percent(42),
    FlexShrink = 0,
    MarginLeft = DimensionValue.Point(1),
    Border = true,
    BorderColor = Rgba.FromHex("#A371F7"),
    Title = "Session / Latest Event",
    BackgroundColor = Rgba.FromHex("#111827"),
    ContentOptions = new BoxOptions
    {
        Padding = DimensionValue.Point(1),
    },
});

var detailText = new TextRenderable(renderer, new TextOptions
{
    Id = "keypress-debug-detail-text",
    Width = DimensionValue.Percent(100),
    WrapMode = WrapMode.Word,
    Fg = Rgba.FromHex("#E6EDF3"),
    Selectable = false,
});
detailFeed.Add(detailText);

var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "keypress-debug-footer",
    Width = DimensionValue.Percent(100),
    Height = DimensionValue.Point(1),
    FlexShrink = 0,
    Fg = Rgba.FromHex("#8B949E"),
    Selectable = false,
});

body.Add(eventFeed);
body.Add(detailFeed);
mainContainer.Add(statusText);
mainContainer.Add(body);
mainContainer.Add(footerText);
renderer.Root.Add(mainContainer);

var helpModal = new BoxRenderable(renderer, new BoxOptions
{
    Id = "keypress-debug-help-modal",
    Position = PositionValue.Absolute,
    Left = DimensionValue.Percent(12),
    Top = DimensionValue.Point(3),
    Width = DimensionValue.Percent(76),
    Height = DimensionValue.Point(16),
    Padding = DimensionValue.Point(1),
    Border = true,
    BorderStyle = BorderStyle.Double,
    BorderColor = Rgba.FromHex("#4ECDC4"),
    BackgroundColor = Rgba.FromHex("#0D1117"),
    Title = "Help",
    Visible = false,
    ZIndex = 100,
});

var helpContent = new TextRenderable(renderer, new TextOptions
{
    Id = "keypress-debug-help-content",
    Width = DimensionValue.Percent(100),
    WrapMode = WrapMode.Word,
    Fg = Rgba.FromHex("#E6EDF3"),
    Selectable = false,
});
helpModal.Add(helpContent);
renderer.Root.Add(helpModal);

renderer.PrependInputHandler(sequence =>
{
    var record = new RawInputRecord
    {
        Timestamp = DateTime.UtcNow.ToString("O"),
        Sequence = sequence,
    };
    allRawInputs.Add(record);
    lastRawInput = record;
    RefreshUi();
    return false;
});

renderer.KeyInput.On<KeyEvent>(KeyHandlerEvents.Keypress, keyEvent =>
{
    if (keyEvent.Raw == "?" && !keyEvent.Ctrl && !keyEvent.Meta && !keyEvent.Super && !keyEvent.Hyper)
    {
        showingHelp = !showingHelp;
        RefreshUi();
        return;
    }

    if (showingHelp && keyEvent.Name == "escape")
    {
        showingHelp = false;
        RefreshUi();
        return;
    }

    if (keyEvent.Shift && string.Equals(keyEvent.Name, "j", StringComparison.OrdinalIgnoreCase))
    {
        showJson = !showJson;
        RefreshUi();
        return;
    }

    if (keyEvent.Shift && string.Equals(keyEvent.Name, "s", StringComparison.OrdinalIgnoreCase))
    {
        _ = SaveToFileAsync();
        return;
    }

    if (keyEvent.Shift && string.Equals(keyEvent.Name, "l", StringComparison.OrdinalIgnoreCase))
    {
        ClearSession();
        return;
    }

    RecordParsedEvent("keypress", SnapshotKeyEvent(keyEvent), paste: null);
});

renderer.KeyInput.On<KeyEvent>(KeyHandlerEvents.Keyrelease, keyEvent =>
{
    RecordParsedEvent("keyrelease", SnapshotKeyEvent(keyEvent), paste: null);
});

renderer.KeyInput.On<PasteEvent>(KeyHandlerEvents.Paste, pasteEvent =>
{
    RecordParsedEvent("paste", key: null, SnapshotPasteEvent(pasteEvent));
});

RefreshUi();
await Task.Delay(Timeout.Infinite);

void RefreshUi()
{
    statusText.ContentText = CreateStatusText();
    eventListText.ContentText = CreateEventListText();
    detailText.ContentText = CreateDetailText();
    footerText.ContentText = "Controls: ?:help  Shift+J:json  Shift+S:save  Shift+L:clear  Ctrl+C:quit";
    helpModal.Visible = showingHelp;
    helpContent.ContentText = CreateHelpText();
    detailFeed.ScrollTop = 0;
    renderer.RequestRender();
}

string CreateStatusText()
{
    string summary = string.Join(" | ",
    [
        "Keypress Debug",
        $"events {allKeyEvents.Count}",
        $"visible {visibleEvents.Count}",
        $"raw {allRawInputs.Count}",
        $"kitty {(renderer.UseKittyKeyboard ? "on" : "off")}",
        $"json {(showJson ? "on" : "off")}",
    ]);

    string feedback = "capture is centered on parsed events; raw input stays in the session detail and export";
    if (!string.IsNullOrEmpty(lastSaveError))
        feedback = $"save failed: {Truncate(lastSaveError, 64)}";
    else if (!string.IsNullOrEmpty(lastSavedFile))
        feedback = $"saved {Truncate(lastSavedFile, 64)}";

    return $"{summary}\nlatest {LatestEventSummary()} | terminal {TerminalSummary()} | {feedback}";
}

string CreateEventListText()
{
    var lines = new List<string>
    {
        "  ID  TIME         TYPE   KEY                NOTES",
        "  --- ------------ ------ ------------------ ----------------------------------------",
    };

    if (visibleEvents.Count == 0)
    {
        lines.Add("  --  waiting for parsed input events --");
        return string.Join('\n', lines);
    }

    for (int index = 0; index < visibleEvents.Count; index++)
        lines.Add(CreateEventRow(visibleEvents[index], index == visibleEvents.Count - 1));

    return string.Join('\n', lines);
}

string CreateEventRow(DebugEntry entry, bool isLatest)
{
    string prefix = isLatest ? ">" : " ";
    string id = entry.Id.ToString("000");
    string time = FormatClock(entry.Timestamp);

    if (entry.Type == "paste")
    {
        var paste = entry.Paste!;
        string preview = Truncate(JsonSerializer.Serialize(paste.Text), 30);
        return $"{prefix} {id} {time} {Pad("paste", 6)} {Pad("Paste", 18)} bytes={paste.ByteLength} text={preview}";
    }

    var key = entry.Key!;
    var notes = new List<string> { $"src={key.Source}" };
    if (key.BaseCode is not null)
        notes.Add($"base={FormatBaseCodeBrief(key.BaseCode)}");
    if (!string.IsNullOrEmpty(key.Raw) && key.Raw != key.Sequence)
        notes.Add($"raw={FormatInline(key.Raw, 20)}");
    else if (!string.IsNullOrEmpty(key.Sequence))
        notes.Add($"seq={FormatInline(key.Sequence, 20)}");
    if (key.Repeated)
        notes.Add("repeat");

    string eventLabel = entry.Type == "keypress" ? "down" : "up";
    return $"{prefix} {id} {time} {Pad(eventLabel, 6)} {Pad(Truncate(FormatCombo(key), 18), 18)} {string.Join(' ', notes)}";
}

string CreateDetailText()
{
    var latest = visibleEvents.LastOrDefault();
    var lines = new List<string>
    {
        "Session",
        $"terminal      {TerminalSummary()}",
        $"kitty kb      {(renderer.UseKittyKeyboard ? "on" : "off")}",
        $"raw inputs    {allRawInputs.Count}",
        $"parsed events {allKeyEvents.Count}",
        $"last raw      {FormatInline(lastRawInput?.Sequence, 56)}",
    };

    if (latest is null)
    {
        lines.Add("");
        lines.Add("Latest Event");
        lines.Add("no parsed event yet");
        return string.Join('\n', lines);
    }

    lines.Add("");
    lines.Add("Latest Event");
    lines.Add($"index         #{latest.Id}");
    lines.Add($"type          {latest.Type}");
    lines.Add($"time          {latest.Timestamp}");

    if (latest.Type == "paste")
    {
        var paste = latest.Paste!;
        lines.Add($"bytes         {paste.ByteLength}");
        lines.Add($"text          {FormatScalar(paste.Text)}");
        lines.Add($"metadata      {FormatPasteMetadata(paste.Metadata)}");
        if (showJson)
        {
            lines.Add("");
            lines.Add("JSON");
            lines.Add(JsonSerializer.Serialize(paste, KeypressDebugJsonContext.Default.PasteSnapshot));
        }

        return string.Join('\n', lines);
    }

    var key = latest.Key!;
    lines.Add($"combo         {FormatCombo(key)}");
    lines.Add($"name          {FormatScalar(key.Name)}");
    lines.Add($"sequence      {FormatScalar(key.Sequence)}");
    lines.Add($"raw           {FormatScalar(key.Raw)}");
    lines.Add($"source        {key.Source}");
    lines.Add($"event type    {key.EventType}");
    lines.Add($"modifiers     {FormatModifiers(key)}");
    lines.Add($"code          {FormatScalar(key.Code)}");
    lines.Add($"base code     {FormatBaseCode(key.BaseCode)}");
    lines.Add($"flags         repeated={(key.Repeated ? "yes" : "no")} caps={(key.CapsLock ? "on" : "off")} num={(key.NumLock ? "on" : "off")} number={(key.Number ? "yes" : "no")}");

    if (showJson)
    {
        lines.Add("");
        lines.Add("JSON");
        lines.Add(JsonSerializer.Serialize(key, KeypressDebugJsonContext.Default.KeySnapshot));
    }

    return string.Join('\n', lines);
}

static string CreateHelpText() => string.Join('\n',
[
    "Keypress Debug",
    "",
    "This demo keeps the feed compact and centers the parsed key events.",
    "Raw input is still captured, but it lives in the session detail and the saved JSON instead of duplicating every row.",
    "",
    "Controls",
    "  ?       toggle help",
    "  Shift+J toggle JSON for the latest parsed event",
    "  Shift+S save the current capture to keypress-debug-*.json",
    "  Shift+L clear the current session",
    "  Ctrl+C  quit",
    "",
    "Tips",
    "  baseCode is shown in the detail pane for Kitty alternate-key events.",
    "  That makes layout and IME issues easier to inspect.",
]);

void RecordParsedEvent(string type, KeySnapshot? key, PasteSnapshot? paste)
{
    eventCount++;
    string timestamp = DateTime.UtcNow.ToString("O");
    var entry = new DebugEntry
    {
        Id = eventCount,
        Timestamp = timestamp,
        Type = type,
        Key = key,
        Paste = paste,
    };

    allKeyEvents.Add(new SavedEventRecord
    {
        Timestamp = timestamp,
        Type = type,
        Key = key,
        Paste = paste,
    });

    visibleEvents.Add(entry);
    while (visibleEvents.Count > 120)
        visibleEvents.RemoveAt(0);

    RefreshUi();
}

void ClearSession()
{
    eventCount = 0;
    allRawInputs = [];
    allKeyEvents = [];
    visibleEvents = [];
    lastRawInput = null;
    lastSavedFile = null;
    lastSaveError = null;
    RefreshUi();
}

async Task SaveToFileAsync()
{
    string timestamp = DateTime.UtcNow.ToString("O").Replace(':', '-').Replace('.', '-');
    string filename = $"keypress-debug-{timestamp}.json";
    var payload = new ExportPayload
    {
        ExportedAt = DateTime.UtcNow.ToString("O"),
        RawInputs = [.. allRawInputs],
        KeyEvents = [.. allKeyEvents],
        Summary = new ExportSummary
        {
            TotalRawInputs = allRawInputs.Count,
            TotalKeyEvents = allKeyEvents.Count,
            VisibleEventWindow = visibleEvents.Count,
        },
        Capabilities = renderer.TerminalCapabilities,
    };

    try
    {
        string json = JsonSerializer.Serialize(payload, KeypressDebugJsonContext.Default.ExportPayload);
        await File.WriteAllTextAsync(filename, json);
        lastSavedFile = filename;
        lastSaveError = null;
    }
    catch (Exception ex)
    {
        lastSavedFile = null;
        lastSaveError = ex.Message;
    }

    RefreshUi();
}

static KeySnapshot SnapshotKeyEvent(KeyEvent keyEvent) => new()
{
    Name = keyEvent.Name,
    Ctrl = keyEvent.Ctrl,
    Meta = keyEvent.Meta,
    Shift = keyEvent.Shift,
    Option = keyEvent.Option,
    Sequence = keyEvent.Sequence,
    Raw = keyEvent.Raw,
    EventType = keyEvent.EventType.ToString().ToLowerInvariant(),
    Source = keyEvent.Source,
    Number = keyEvent.Number,
    Code = keyEvent.Code,
    Super = keyEvent.Super,
    Hyper = keyEvent.Hyper,
    CapsLock = keyEvent.CapsLock,
    NumLock = keyEvent.NumLock,
    BaseCode = keyEvent.BaseCode,
    Repeated = keyEvent.Repeated,
};

static PasteSnapshot SnapshotPasteEvent(PasteEvent pasteEvent) => new()
{
    ByteLength = pasteEvent.Bytes.Length,
    Bytes = [.. pasteEvent.Bytes],
    Text = pasteEvent.Text,
    Metadata = pasteEvent.Metadata,
};

string TerminalSummary()
{
    var caps = renderer.TerminalCapabilities;
    if (caps is null || string.IsNullOrWhiteSpace(caps.TermName))
        return "unknown";

    return string.IsNullOrWhiteSpace(caps.TermVersion)
        ? caps.TermName
        : $"{caps.TermName} {caps.TermVersion}";
}

string LatestEventSummary()
{
    var latest = visibleEvents.LastOrDefault();
    if (latest is null)
        return "none";

    return latest.Type == "paste"
        ? $"paste {latest.Paste!.ByteLength}B"
        : FormatCombo(latest.Key!);
}

static string FormatClock(string timestamp) =>
    DateTimeOffset.Parse(timestamp).ToLocalTime().ToString("HH:mm:ss.fff");

static string Truncate(string text, int maxLength) =>
    maxLength <= 3 || text.Length <= maxLength ? text : $"{text[..(maxLength - 3)]}...";

static string Pad(string text, int width) => text.Length >= width ? text : text.PadRight(width);

static string FormatScalar(string? value) => value is null ? "null" : JsonSerializer.Serialize(value);

static string FormatInline(string? text, int maxLength) => Truncate(FormatScalar(text), maxLength);

static string FormatPasteMetadata(PasteMetadata? metadata) =>
    metadata is null ? "null" : $"{{ kind: {metadata.Kind}, mimeType: {metadata.MimeType ?? "null"} }}";

static string FormatCombo(KeySnapshot snapshot)
{
    string modifiers = FormatModifiers(snapshot);
    string name = FormatCharName(snapshot.Name);
    if (modifiers == "-")
        return name;
    if (name == "-")
        return modifiers;
    return $"{modifiers}+{name}";
}

static string FormatModifiers(KeySnapshot snapshot)
{
    List<string> modifiers = [];
    if (snapshot.Ctrl) modifiers.Add("Ctrl");
    if (snapshot.Meta) modifiers.Add("Meta");
    if (snapshot.Shift) modifiers.Add("Shift");
    if (snapshot.Option) modifiers.Add("Option");
    if (snapshot.Super) modifiers.Add("Super");
    if (snapshot.Hyper) modifiers.Add("Hyper");
    return modifiers.Count == 0 ? "-" : string.Join('+', modifiers);
}

static string FormatCharName(string name) => name switch
{
    " " or "space" => "Space",
    "escape" => "Escape",
    "return" => "Return",
    "linefeed" => "Linefeed",
    "backspace" => "Backspace",
    "tab" => "Tab",
    _ => name,
};

static string FormatBaseCode(int? baseCode)
{
    if (baseCode is null)
        return "-";

    if (baseCode >= 32 && baseCode != 127)
        return $"{baseCode} ({JsonSerializer.Serialize(char.ConvertFromUtf32(baseCode.Value))})";

    return $"{baseCode} (U+{baseCode:X4})";
}

static string FormatBaseCodeBrief(int? baseCode)
{
    if (baseCode is null)
        return "-";

    if (baseCode >= 32 && baseCode != 127)
        return JsonSerializer.Serialize(char.ConvertFromUtf32(baseCode.Value));

    return $"U+{baseCode:X4}";
}

public sealed class RawInputRecord
{
    public required string Timestamp { get; init; }
    public required string Sequence { get; init; }
}

public sealed class KeySnapshot
{
    public required string Name { get; init; }
    public bool Ctrl { get; init; }
    public bool Meta { get; init; }
    public bool Shift { get; init; }
    public bool Option { get; init; }
    public required string Sequence { get; init; }
    public required string Raw { get; init; }
    public required string EventType { get; init; }
    public required string Source { get; init; }
    public bool Number { get; init; }
    public string? Code { get; init; }
    public bool Super { get; init; }
    public bool Hyper { get; init; }
    public bool CapsLock { get; init; }
    public bool NumLock { get; init; }
    public int? BaseCode { get; init; }
    public bool Repeated { get; init; }
}

public sealed class PasteSnapshot
{
    public int ByteLength { get; init; }
    public required byte[] Bytes { get; init; }
    public required string Text { get; init; }
    public PasteMetadata? Metadata { get; init; }
}

public sealed class DebugEntry
{
    public int Id { get; init; }
    public required string Timestamp { get; init; }
    public required string Type { get; init; }
    public KeySnapshot? Key { get; init; }
    public PasteSnapshot? Paste { get; init; }
}

public sealed class SavedEventRecord
{
    public required string Timestamp { get; init; }
    public required string Type { get; init; }
    public KeySnapshot? Key { get; init; }
    public PasteSnapshot? Paste { get; init; }
}

public sealed class ExportSummary
{
    public int TotalRawInputs { get; init; }
    public int TotalKeyEvents { get; init; }
    public int VisibleEventWindow { get; init; }
}

public sealed class ExportPayload
{
    public required string ExportedAt { get; init; }
    public required RawInputRecord[] RawInputs { get; init; }
    public required SavedEventRecord[] KeyEvents { get; init; }
    public required ExportSummary Summary { get; init; }
    public TerminalCapabilities? Capabilities { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ExportPayload))]
[JsonSerializable(typeof(KeySnapshot))]
[JsonSerializable(typeof(PasteSnapshot))]
internal sealed partial class KeypressDebugJsonContext : JsonSerializerContext
{
}
