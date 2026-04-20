using System.Text;
using System.Text.RegularExpressions;

namespace OpenTui.Core;

/// <summary>
/// Protocol for stdin response events (CSI, OSC, DCS, APC, unknown).
/// </summary>
public enum StdinResponseProtocol : byte
{
    /// <summary>
    /// Represents the Unknown option.
    /// </summary>
    Unknown,
    /// <summary>
    /// Represents the Csi option.
    /// </summary>
    Csi,
    /// <summary>
    /// Represents the Osc option.
    /// </summary>
    Osc,
    /// <summary>
    /// Represents the Dcs option.
    /// </summary>
    Dcs,
    /// <summary>
    /// Represents the Apc option.
    /// </summary>
    Apc,
}

/// <summary>
/// Protocol context flags that control how the parser interprets certain sequences.
/// </summary>
public sealed class ProtocolContext
{
    /// <summary>
    /// Gets or sets the kitty keyboard enabled.
    /// </summary>
    public bool KittyKeyboardEnabled { get; set; }
    /// <summary>
    /// Gets or sets the private capability replies active.
    /// </summary>
    public bool PrivateCapabilityRepliesActive { get; set; }
    /// <summary>
    /// Gets or sets the pixel resolution query active.
    /// </summary>
    public bool PixelResolutionQueryActive { get; set; }
    /// <summary>
    /// Gets or sets the explicit width cpr active.
    /// </summary>
    public bool ExplicitWidthCprActive { get; set; }

    internal ProtocolContext Clone() => new()
    {
        KittyKeyboardEnabled = KittyKeyboardEnabled,
        PrivateCapabilityRepliesActive = PrivateCapabilityRepliesActive,
        PixelResolutionQueryActive = PixelResolutionQueryActive,
        ExplicitWidthCprActive = ExplicitWidthCprActive,
    };
}

/// <summary>
/// Options for constructing a <see cref="StdinParser"/>.
/// </summary>
public sealed class StdinParserOptions
{
    /// <summary>
    /// Gets or sets the timeout ms.
    /// </summary>
    public int? TimeoutMs { get; set; }
    /// <summary>
    /// Gets or sets the max pending bytes.
    /// </summary>
    public int? MaxPendingBytes { get; set; }
    /// <summary>
    /// Gets or sets the arm timeouts.
    /// </summary>
    public bool ArmTimeouts { get; set; } = true;
    /// <summary>
    /// Gets or sets the on timeout flush.
    /// </summary>
    public Action? OnTimeoutFlush { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether use kitty keyboard.
    /// </summary>
    public bool UseKittyKeyboard { get; set; } = true;
    /// <summary>
    /// Gets or sets the protocol context.
    /// </summary>
    public ProtocolContext? ProtocolContext { get; set; }
}

/// <summary>
/// Push-driven stdin parser. Callers feed raw bytes via <see cref="Push"/>,
/// then read typed events via <see cref="Read"/> or <see cref="Drain"/>.
/// Faithful 1:1 port of the TypeScript StdinParser.
/// </summary>
public sealed partial class StdinParser
{
    // ── Constants ────────────────────────────────────────────────────────
    private const int DefaultTimeoutMs = 20;
    private const int DefaultMaxPendingBytes = 64 * 1024;
    private const int InitialPendingCapacity = 256;
    private const byte ESC = 0x1B;
    private const byte BEL = 0x07;

    private static readonly byte[] BracketedPasteStart = "\x1b[200~"u8.ToArray();
    private static readonly byte[] BracketedPasteEnd = "\x1b[201~"u8.ToArray();

    [GeneratedRegex(@"^\x1b\[\d+\$$")]
    private static partial Regex RxvtDollarCsiRe();

    // ── Fields ───────────────────────────────────────────────────────────
    private readonly ByteQueue _pending = new(InitialPendingCapacity);
    private readonly Queue<StdinEvent> _events = new();
    private readonly int _timeoutMs;
    private readonly int _maxPendingBytes;
    private readonly bool _armTimeouts;
    private readonly Action? _onTimeoutFlush;
    private readonly bool _useKittyKeyboard;
    private readonly MouseParser _mouseParser = new();
    private ProtocolContext _protocolContext;
    private bool _destroyed;
    private long? _pendingSinceMs;
    private bool _forceFlush;
    private bool _justFlushedEsc;
    private ParserState _state;
    private int _cursor;
    private int _unitStart;
    private PasteCollector? _paste;

    // ── Constructor ──────────────────────────────────────────────────────
    /// <summary>
    /// Initializes a new instance of the StdinParser class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public StdinParser(StdinParserOptions? options = null)
    {
        options ??= new StdinParserOptions();
        _timeoutMs = NormalizePositiveOption(options.TimeoutMs, DefaultTimeoutMs);
        _maxPendingBytes = NormalizePositiveOption(options.MaxPendingBytes, DefaultMaxPendingBytes);
        _armTimeouts = options.ArmTimeouts;
        _onTimeoutFlush = options.OnTimeoutFlush;
        _useKittyKeyboard = options.UseKittyKeyboard;
        _protocolContext = options.ProtocolContext?.Clone() ?? new ProtocolContext();
        _state = ParserState.Ground();
    }

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>Current internal buffer capacity.</summary>
    public int BufferCapacity => _pending.Capacity;

    /// <summary>Updates protocol context flags and reconciles deferred state.</summary>
    public void UpdateProtocolContext(ProtocolContext patch)
    {
        EnsureAlive();
        if (patch.KittyKeyboardEnabled) _protocolContext.KittyKeyboardEnabled = true;
        else _protocolContext.KittyKeyboardEnabled = patch.KittyKeyboardEnabled;
        if (patch.PrivateCapabilityRepliesActive) _protocolContext.PrivateCapabilityRepliesActive = true;
        else _protocolContext.PrivateCapabilityRepliesActive = patch.PrivateCapabilityRepliesActive;
        if (patch.PixelResolutionQueryActive) _protocolContext.PixelResolutionQueryActive = true;
        else _protocolContext.PixelResolutionQueryActive = patch.PixelResolutionQueryActive;
        if (patch.ExplicitWidthCprActive) _protocolContext.ExplicitWidthCprActive = true;
        else _protocolContext.ExplicitWidthCprActive = patch.ExplicitWidthCprActive;
        ReconcileDeferredStateWithProtocolContext();
        ReconcileTimeoutState();
    }

    /// <summary>
    /// Replaces the entire protocol context and reconciles deferred state.
    /// </summary>
    public void SetProtocolContext(ProtocolContext ctx)
    {
        EnsureAlive();
        _protocolContext = ctx.Clone();
        ReconcileDeferredStateWithProtocolContext();
        ReconcileTimeoutState();
    }

    /// <summary>Feeds raw stdin bytes into the parser.</summary>
    public void Push(ReadOnlySpan<byte> data)
    {
        EnsureAlive();
        if (data.Length == 0)
        {
            EmitKeyOrResponse(StdinResponseProtocol.Unknown, "");
            return;
        }

        // We need to copy into a byte[] for the processing loop since we
        // may need to slice multiple times.
        var remainder = data.ToArray().AsSpan();
        while (remainder.Length > 0)
        {
            if (_paste is not null)
            {
                remainder = ConsumePasteBytes(remainder);
                continue;
            }

            int immediatePasteStartIndex =
                _state.Tag == ParserStateTag.Ground && _pending.Length == 0
                    ? IndexOfBytes(remainder, BracketedPasteStart)
                    : -1;
            int appendEnd =
                immediatePasteStartIndex == -1
                    ? remainder.Length
                    : immediatePasteStartIndex + BracketedPasteStart.Length;

            _pending.Append(remainder[..appendEnd]);
            remainder = remainder[appendEnd..];
            ScanPending();

            if (_paste is not null && _pending.Length > 0)
            {
                var leftover = _pending.Take();
                remainder = ConcatBytes(ConsumePasteBytes(leftover), remainder);
                continue;
            }

            if (_paste is null && _pending.Length > _maxPendingBytes)
            {
                FlushPendingOverflow();
                ScanPending();

                if (_paste is not null && _pending.Length > 0)
                {
                    var leftover = _pending.Take();
                    remainder = ConcatBytes(ConsumePasteBytes(leftover), remainder);
                }
            }
        }

        ReconcileTimeoutState();
    }

    /// <summary>Pops one event from the queue.</summary>
    public StdinEvent? Read()
    {
        EnsureAlive();

        if (_events.Count == 0 && _forceFlush)
        {
            ScanPending();
            ReconcileTimeoutState();
        }

        return _events.Count > 0 ? _events.Dequeue() : null;
    }

    /// <summary>Delivers all queued events.</summary>
    public void Drain(Action<StdinEvent> onEvent)
    {
        EnsureAlive();

        while (true)
        {
            if (_destroyed) return;

            var evt = Read();
            if (evt is null) return;

            onEvent(evt);
        }
    }

    /// <summary>
    /// Marks the parser for forced flush if enough time has passed since
    /// incomplete data arrived.
    /// </summary>
    public void FlushTimeout(long? nowMs = null)
    {
        EnsureAlive();

        long now = nowMs ?? Environment.TickCount64;

        if (_pendingSinceMs is not null &&
            (now < _pendingSinceMs.Value || now - _pendingSinceMs.Value < _timeoutMs))
        {
            return;
        }

        TryForceFlush();
    }

    /// <summary>Resets all parser state.</summary>
    public void Reset()
    {
        if (_destroyed) return;
        ResetState();
    }

    /// <summary>Clears tracked mouse button state.</summary>
    public void ResetMouseState()
    {
        EnsureAlive();
        _mouseParser.Reset();
    }

    /// <summary>Destroys the parser, preventing further use.</summary>
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        ResetState();
    }

    // ── Parser state enum ────────────────────────────────────────────────

    private enum ParserStateTag : byte
    {
        Ground,
        Utf8,
        Esc,
        Ss3,
        Csi,
        CsiSgrMouse,
        CsiSgrMouseDeferred,
        CsiParametric,
        CsiParametricDeferred,
        CsiPrivateReply,
        CsiPrivateReplyDeferred,
        Osc,
        Dcs,
        Apc,
        EscRecovery,
        EscLessMouse,
        EscLessX10Mouse,
    }

    /// <summary>
    /// Discriminated state for the parser state machine.
    /// Uses a struct to avoid allocations on every state transition.
    /// </summary>
    private struct ParserState
    {
        public ParserStateTag Tag;

        // utf8 fields
        public int Expected;
        public int Seen;

        // csi_sgr_mouse / csi_sgr_mouse_deferred fields
        public int Part;
        public bool HasDigit;

        // csi_parametric / csi_parametric_deferred fields
        public int Semicolons;
        public int Segments;
        public int? FirstParamValue;

        // csi_private_reply / csi_private_reply_deferred fields
        public bool SawDollar;

        // osc / dcs / apc fields
        public bool SawEsc;

        public static ParserState Ground() => new() { Tag = ParserStateTag.Ground };
        public static ParserState Utf8(int expected, int seen) => new() { Tag = ParserStateTag.Utf8, Expected = expected, Seen = seen };
        public static ParserState Esc() => new() { Tag = ParserStateTag.Esc };
        public static ParserState Ss3() => new() { Tag = ParserStateTag.Ss3 };
        public static ParserState CsiState() => new() { Tag = ParserStateTag.Csi };
        public static ParserState CsiSgrMouse(int part, bool hasDigit) => new() { Tag = ParserStateTag.CsiSgrMouse, Part = part, HasDigit = hasDigit };
        public static ParserState CsiSgrMouseDeferred(int part, bool hasDigit) => new() { Tag = ParserStateTag.CsiSgrMouseDeferred, Part = part, HasDigit = hasDigit };
        public static ParserState CsiParametric(int semicolons, int segments, bool hasDigit, int? firstParamValue) =>
            new() { Tag = ParserStateTag.CsiParametric, Semicolons = semicolons, Segments = segments, HasDigit = hasDigit, FirstParamValue = firstParamValue };
        public static ParserState CsiParametricDeferred(int semicolons, int segments, bool hasDigit, int? firstParamValue) =>
            new() { Tag = ParserStateTag.CsiParametricDeferred, Semicolons = semicolons, Segments = segments, HasDigit = hasDigit, FirstParamValue = firstParamValue };
        public static ParserState CsiPrivateReply(int semicolons, bool hasDigit, bool sawDollar) =>
            new() { Tag = ParserStateTag.CsiPrivateReply, Semicolons = semicolons, HasDigit = hasDigit, SawDollar = sawDollar };
        public static ParserState CsiPrivateReplyDeferred(int semicolons, bool hasDigit, bool sawDollar) =>
            new() { Tag = ParserStateTag.CsiPrivateReplyDeferred, Semicolons = semicolons, HasDigit = hasDigit, SawDollar = sawDollar };
        public static ParserState OscState(bool sawEsc) => new() { Tag = ParserStateTag.Osc, SawEsc = sawEsc };
        public static ParserState DcsState(bool sawEsc) => new() { Tag = ParserStateTag.Dcs, SawEsc = sawEsc };
        public static ParserState ApcState(bool sawEsc) => new() { Tag = ParserStateTag.Apc, SawEsc = sawEsc };
        public static ParserState EscRecovery() => new() { Tag = ParserStateTag.EscRecovery };
        public static ParserState EscLessMouse() => new() { Tag = ParserStateTag.EscLessMouse };
        public static ParserState EscLessX10Mouse() => new() { Tag = ParserStateTag.EscLessX10Mouse };
    }

    // ── PasteCollector ───────────────────────────────────────────────────

    private sealed class PasteCollector
    {
        public byte[] Tail = [];
        public readonly List<byte[]> Parts = [];
        public int TotalLength;
    }

    // ── ByteQueue ────────────────────────────────────────────────────────

    /// <summary>
    /// Ring-style byte buffer with start/end offsets. Compacts when consumed
    /// prefix exceeds half the buffer.
    /// </summary>
    internal sealed class ByteQueue
    {
        private byte[] _buf;
        private int _start;
        private int _end;

        public ByteQueue(int capacity = InitialPendingCapacity)
        {
            _buf = new byte[capacity];
        }

        public int Length => _end - _start;
        public int Capacity => _buf.Length;

        /// <summary>Returns a span view of the current contents.</summary>
        public ReadOnlySpan<byte> View() => _buf.AsSpan(_start, _end - _start);

        /// <summary>Returns the contents as an array and resets the queue.</summary>
        public byte[] Take()
        {
            var chunk = _buf.AsSpan(_start, _end - _start).ToArray();
            _start = 0;
            _end = 0;
            return chunk;
        }

        public void Append(ReadOnlySpan<byte> chunk)
        {
            if (chunk.Length == 0) return;

            EnsureCapacity(Length + chunk.Length);
            chunk.CopyTo(_buf.AsSpan(_end));
            _end += chunk.Length;
        }

        /// <summary>
        /// Drops the first <paramref name="count"/> bytes. Compacts when consumed
        /// prefix exceeds half the buffer.
        /// </summary>
        public void Consume(int count)
        {
            if (count <= 0) return;

            if (count >= Length)
            {
                _start = 0;
                _end = 0;
                return;
            }

            _start += count;
            if (_start >= _buf.Length / 2)
            {
                Array.Copy(_buf, _start, _buf, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }
        }

        public void Clear()
        {
            _start = 0;
            _end = 0;
        }

        public void Reset(int capacity = InitialPendingCapacity)
        {
            _buf = new byte[capacity];
            _start = 0;
            _end = 0;
        }

        private void EnsureCapacity(int requiredLength)
        {
            int currentLength = Length;
            if (requiredLength <= _buf.Length)
            {
                int availableAtEnd = _buf.Length - _end;
                if (availableAtEnd >= requiredLength - currentLength)
                    return;

                Array.Copy(_buf, _start, _buf, 0, _end - _start);
                _end = currentLength;
                _start = 0;
                if (requiredLength <= _buf.Length)
                    return;
            }

            int nextCapacity = _buf.Length;
            while (nextCapacity < requiredLength)
                nextCapacity *= 2;

            var next = new byte[nextCapacity];
            View().CopyTo(next);
            _buf = next;
            _start = 0;
            _end = currentLength;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static int NormalizePositiveOption(int? value, int fallback)
    {
        if (value is null || value.Value <= 0) return fallback;
        return value.Value;
    }

    private static int Utf8SequenceLength(byte first)
    {
        if (first < 0x80) return 1;
        if (first >= 0xC2 && first <= 0xDF) return 2;
        if (first >= 0xE0 && first <= 0xEF) return 3;
        if (first >= 0xF0 && first <= 0xF4) return 4;
        return 0;
    }

    private static bool IsAsciiDigit(byte b) => b >= 0x30 && b <= 0x39;

    private static bool BytesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) => left.SequenceEqual(right);

    private static bool IsMouseSgrSequence(ReadOnlySpan<byte> sequence)
    {
        if (sequence.Length < 7) return false;
        if (sequence[0] != ESC || sequence[1] != 0x5B || sequence[2] != 0x3C) return false;

        byte final_ = sequence[sequence.Length - 1];
        if (final_ != 0x4D && final_ != 0x6D) return false;

        int part = 0;
        bool hasDigit = false;
        for (int i = 3; i < sequence.Length - 1; i++)
        {
            byte b = sequence[i];
            if (b >= 0x30 && b <= 0x39) { hasDigit = true; continue; }
            if (b == 0x3B && hasDigit && part < 2) { part++; hasDigit = false; continue; }
            return false;
        }

        return part == 2 && hasDigit;
    }

    private static int? ParsePositiveDecimalPrefix(ReadOnlySpan<byte> sequence, int start, int endExclusive)
    {
        if (start >= endExclusive) return null;

        int value = 0;
        bool sawDigit = false;
        for (int i = start; i < endExclusive; i++)
        {
            byte b = sequence[i];
            if (!IsAsciiDigit(b)) return null;
            sawDigit = true;
            value = value * 10 + (b - 0x30);
        }

        return sawDigit ? value : null;
    }

    private static int? ParseKittyFirstFieldCodepoint(ReadOnlySpan<byte> sequence, int start, int endExclusive)
    {
        if (start >= endExclusive) return null;

        int firstColon = -1;
        for (int i = start; i < endExclusive; i++)
        {
            if (sequence[i] == 0x3A) { firstColon = i; break; }
        }
        if (firstColon == -1) return null;

        int? codepoint = ParsePositiveDecimalPrefix(sequence, start, firstColon);
        if (codepoint is null) return null;

        for (int i = firstColon + 1; i < endExclusive; i++)
        {
            byte b = sequence[i];
            if (b != 0x3A && !IsAsciiDigit(b)) return null;
        }

        return codepoint;
    }

    private static bool CanStillBeKittyU(in ParserState state) => state.Semicolons >= 1;
    private static bool CanStillBeKittySpecial(in ParserState state) => state.Semicolons == 1 && state.Segments > 1;
    private static bool CanStillBeExplicitWidthCpr(in ParserState state) => state.FirstParamValue == 1 && state.Semicolons == 1;
    private static bool CanStillBePixelResolution(in ParserState state) => state.FirstParamValue == 4 && state.Semicolons == 2;

    private static bool CanDeferParametricCsi(in ParserState state, ProtocolContext context) =>
        (context.KittyKeyboardEnabled && (CanStillBeKittyU(state) || CanStillBeKittySpecial(state))) ||
        (context.ExplicitWidthCprActive && CanStillBeExplicitWidthCpr(state)) ||
        (context.PixelResolutionQueryActive && CanStillBePixelResolution(state));

    private static bool CanCompleteDeferredParametricCsi(in ParserState state, byte b, ProtocolContext context)
    {
        if (context.KittyKeyboardEnabled)
        {
            if (state.HasDigit && b == 0x75) return true;
            if (state.HasDigit && state.Semicolons == 1 && state.Segments > 1 &&
                (b == 0x7E || (b >= 0x41 && b <= 0x5A)))
                return true;
        }
        if (context.ExplicitWidthCprActive && state.HasDigit && state.FirstParamValue == 1 &&
            state.Semicolons == 1 && b == 0x52)
            return true;
        if (context.PixelResolutionQueryActive && state.HasDigit && state.FirstParamValue == 4 &&
            state.Semicolons == 2 && b == 0x74)
            return true;
        return false;
    }

    private static bool CanDeferPrivateReplyCsi(ProtocolContext context) => context.PrivateCapabilityRepliesActive;

    private static bool CanCompleteDeferredPrivateReplyCsi(in ParserState state, byte b, ProtocolContext context)
    {
        if (!context.PrivateCapabilityRepliesActive) return false;
        if (state.SawDollar) return state.HasDigit && b == 0x79;
        if (b == 0x63) return state.HasDigit || state.Semicolons > 0;
        return state.HasDigit && b == 0x75;
    }

    private static byte[] ConcatBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length == 0) return right.ToArray();
        if (right.Length == 0) return left.ToArray();
        var combined = new byte[left.Length + right.Length];
        left.CopyTo(combined);
        right.CopyTo(combined.AsSpan(left.Length));
        return combined;
    }

    private static int IndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0) return 0;
        int limit = haystack.Length - needle.Length;
        for (int offset = 0; offset <= limit; offset++)
        {
            if (haystack.Slice(offset, needle.Length).SequenceEqual(needle))
                return offset;
        }
        return -1;
    }

    private static string DecodeLatin1(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

    private static string DecodeUtf8(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    private static byte[] JoinPasteBytes(List<byte[]> parts, int totalLength)
    {
        if (totalLength == 0) return [];
        if (parts.Count == 1) return parts[0];
        var bytes = new byte[totalLength];
        int offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(bytes, offset);
            offset += part.Length;
        }
        return bytes;
    }

    // ── Ensure alive ─────────────────────────────────────────────────────

    private void EnsureAlive()
    {
        if (_destroyed) throw new ObjectDisposedException(nameof(StdinParser), "StdinParser has been destroyed");
    }

    // ── Core scan loop ───────────────────────────────────────────────────

    private void ScanPending()
    {
        while (_paste is null)
        {
            var bytes = _pending.View();
            if (_state.Tag == ParserStateTag.Ground && _cursor >= bytes.Length)
            {
                _pending.Clear();
                _cursor = 0;
                _unitStart = 0;
                _pendingSinceMs = null;
                _forceFlush = false;
                return;
            }

            int b = _cursor < bytes.Length ? bytes[_cursor] : -1;

            switch (_state.Tag)
            {
                case ParserStateTag.Ground:
                {
                    _unitStart = _cursor;

                    if (_justFlushedEsc)
                    {
                        if (b == 0x5B)
                        {
                            _justFlushedEsc = false;
                            _cursor++;
                            _state = ParserState.EscRecovery();
                            continue;
                        }
                        _justFlushedEsc = false;
                    }

                    if (b == ESC)
                    {
                        _cursor++;
                        _state = ParserState.Esc();
                        continue;
                    }

                    if (b < 0x80)
                    {
                        EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes.Slice(_cursor, 1)));
                        ConsumePrefix(_cursor + 1);
                        continue;
                    }

                    int expected = Utf8SequenceLength((byte)b);
                    if (expected == 0)
                    {
                        if (!_forceFlush && _cursor + 1 == bytes.Length)
                        {
                            MarkPending();
                            return;
                        }
                        EmitLegacyHighByte((byte)b);
                        ConsumePrefix(_cursor + 1);
                        continue;
                    }

                    _cursor++;
                    _state = ParserState.Utf8(expected, 1);
                    continue;
                }

                case ParserStateTag.Utf8:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitLegacyHighByte(bytes[_unitStart]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_unitStart + 1);
                        continue;
                    }

                    if (((byte)b & 0xC0) != 0x80)
                    {
                        EmitLegacyHighByte(bytes[_unitStart]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_unitStart + 1);
                        continue;
                    }

                    int nextSeen = _state.Seen + 1;
                    _cursor++;
                    if (nextSeen < _state.Expected)
                    {
                        _state = ParserState.Utf8(_state.Expected, nextSeen);
                        continue;
                    }

                    EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes[_unitStart.._cursor]));
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                    continue;
                }

                case ParserStateTag.Esc:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }

                        bool flushedLoneEsc = _cursor == _unitStart + 1 && bytes[_unitStart] == ESC;
                        EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes[_unitStart.._cursor]));
                        _justFlushedEsc = flushedLoneEsc;
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    switch ((byte)b)
                    {
                        case 0x5B: // [
                            _cursor++;
                            _state = ParserState.CsiState();
                            continue;
                        case 0x4F: // O
                            _cursor++;
                            _state = ParserState.Ss3();
                            continue;
                        case 0x5D: // ]
                            _cursor++;
                            _state = ParserState.OscState(false);
                            continue;
                        case 0x50: // P
                            _cursor++;
                            _state = ParserState.DcsState(false);
                            continue;
                        case 0x5F: // _
                            _cursor++;
                            _state = ParserState.ApcState(false);
                            continue;
                        case ESC: // ESC ESC
                            _cursor++;
                            continue;
                        default:
                            _cursor++;
                            EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes[_unitStart.._cursor]));
                            _state = ParserState.Ground();
                            ConsumePrefix(_cursor);
                            continue;
                    }
                }

                case ParserStateTag.Ss3:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    _cursor++;
                    EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes[_unitStart.._cursor]));
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                    continue;
                }

                case ParserStateTag.EscRecovery:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes[_unitStart.._cursor]));
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (b == 0x3C)
                    {
                        _cursor++;
                        _state = ParserState.EscLessMouse();
                        continue;
                    }

                    if (b == 0x4D)
                    {
                        _cursor++;
                        _state = ParserState.EscLessX10Mouse();
                        continue;
                    }

                    EmitKeyOrResponse(StdinResponseProtocol.Unknown, DecodeUtf8(bytes.Slice(_unitStart, 1)));
                    _state = ParserState.Ground();
                    ConsumePrefix(_unitStart + 1);
                    continue;
                }

                case ParserStateTag.Csi:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    // X10 mouse: ESC [ M plus 3 raw payload bytes
                    if (b == 0x4D && _cursor == _unitStart + 2)
                    {
                        int end = _cursor + 4;
                        if (bytes.Length < end)
                        {
                            if (!_forceFlush) { MarkPending(); return; }
                            EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart..bytes.Length]);
                            _state = ParserState.Ground();
                            ConsumePrefix(bytes.Length);
                            continue;
                        }
                        EmitMouse(bytes[_unitStart..end], MouseEncoding.X10);
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    // rxvt $-terminated CSI
                    if (b == 0x24)
                    {
                        int candidateEnd = _cursor + 1;
                        string candidate = DecodeUtf8(bytes[_unitStart..candidateEnd]);
                        if (RxvtDollarCsiRe().IsMatch(candidate))
                        {
                            EmitKeyOrResponse(StdinResponseProtocol.Csi, candidate);
                            _state = ParserState.Ground();
                            ConsumePrefix(candidateEnd);
                            continue;
                        }
                        if (!_forceFlush && candidateEnd >= bytes.Length)
                        {
                            MarkPending();
                            return;
                        }
                    }

                    // SGR mouse: ESC [ <
                    if (b == 0x3C && _cursor == _unitStart + 2)
                    {
                        _cursor++;
                        _state = ParserState.CsiSgrMouse(0, false);
                        continue;
                    }

                    // ESC [[
                    if (b == 0x5B && _cursor == _unitStart + 2)
                    {
                        _cursor++;
                        continue;
                    }

                    // CSI ? private
                    if (b == 0x3F && _cursor == _unitStart + 2)
                    {
                        _cursor++;
                        _state = ParserState.CsiPrivateReply(0, false, false);
                        continue;
                    }

                    // Semicolon -> parametric
                    if (b == 0x3B)
                    {
                        int firstParamStart = _unitStart + 2;
                        int firstParamEnd = _cursor;
                        int? firstParamValue = ParsePositiveDecimalPrefix(bytes, firstParamStart, firstParamEnd);

                        if (firstParamValue is null && _protocolContext.KittyKeyboardEnabled)
                            firstParamValue = ParseKittyFirstFieldCodepoint(bytes, firstParamStart, firstParamEnd);

                        if (firstParamValue is not null)
                        {
                            _cursor++;
                            _state = ParserState.CsiParametric(1, 1, false, firstParamValue);
                            continue;
                        }
                    }

                    // Standard CSI final byte (0x40–0x7E)
                    if (b >= 0x40 && b <= 0x7E)
                    {
                        int end = _cursor + 1;
                        var rawBytes = bytes[_unitStart..end];

                        if (BytesEqual(rawBytes, BracketedPasteStart))
                        {
                            _state = ParserState.Ground();
                            ConsumePrefix(end);
                            _paste = new PasteCollector();
                            continue;
                        }

                        if (IsMouseSgrSequence(rawBytes))
                        {
                            EmitMouse(rawBytes, MouseEncoding.Sgr);
                            _state = ParserState.Ground();
                            ConsumePrefix(end);
                            continue;
                        }

                        EmitKeyOrResponse(StdinResponseProtocol.Csi, DecodeUtf8(rawBytes));
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    _cursor++;
                    continue;
                }

                case ParserStateTag.CsiSgrMouse:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        _state = ParserState.CsiSgrMouseDeferred(_state.Part, _state.HasDigit);
                        _pendingSinceMs = null;
                        _forceFlush = false;
                        return;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (IsAsciiDigit((byte)b))
                    {
                        _cursor++;
                        _state = ParserState.CsiSgrMouse(_state.Part, true);
                        continue;
                    }

                    if (b == 0x3B && _state.HasDigit && _state.Part < 2)
                    {
                        _cursor++;
                        _state = ParserState.CsiSgrMouse(_state.Part + 1, false);
                        continue;
                    }

                    if (b >= 0x40 && b <= 0x7E)
                    {
                        int end = _cursor + 1;
                        var rawBytes = bytes[_unitStart..end];
                        if (IsMouseSgrSequence(rawBytes))
                            EmitMouse(rawBytes, MouseEncoding.Sgr);
                        else
                            EmitKeyOrResponse(StdinResponseProtocol.Csi, DecodeUtf8(rawBytes));
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    _state = ParserState.CsiState();
                    continue;
                }

                case ParserStateTag.CsiSgrMouseDeferred:
                {
                    if (_cursor >= bytes.Length)
                    {
                        _pendingSinceMs = null;
                        _forceFlush = false;
                        return;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (IsAsciiDigit((byte)b) || b == 0x3B || b == 0x4D || b == 0x6D)
                    {
                        _state = ParserState.CsiSgrMouse(_state.Part, _state.HasDigit);
                        continue;
                    }

                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                    continue;
                }

                case ParserStateTag.CsiParametric:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }

                        if (CanDeferParametricCsi(_state, _protocolContext))
                        {
                            _state = ParserState.CsiParametricDeferred(
                                _state.Semicolons, _state.Segments, _state.HasDigit, _state.FirstParamValue);
                            _pendingSinceMs = null;
                            _forceFlush = false;
                            return;
                        }

                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (IsAsciiDigit((byte)b))
                    {
                        _cursor++;
                        _state = ParserState.CsiParametric(
                            _state.Semicolons, _state.Segments, true, _state.FirstParamValue);
                        continue;
                    }

                    if (b == 0x3A && _state.HasDigit && _state.Segments < 3)
                    {
                        _cursor++;
                        _state = ParserState.CsiParametric(
                            _state.Semicolons, _state.Segments + 1, false, _state.FirstParamValue);
                        continue;
                    }

                    if (b == 0x3B && _state.Semicolons < 2)
                    {
                        _cursor++;
                        _state = ParserState.CsiParametric(
                            _state.Semicolons + 1, 1, false, _state.FirstParamValue);
                        continue;
                    }

                    if (b >= 0x40 && b <= 0x7E)
                    {
                        int end = _cursor + 1;
                        EmitKeyOrResponse(StdinResponseProtocol.Csi, DecodeUtf8(bytes[_unitStart..end]));
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    _state = ParserState.CsiState();
                    continue;
                }

                case ParserStateTag.CsiParametricDeferred:
                {
                    if (_cursor >= bytes.Length)
                    {
                        _pendingSinceMs = null;
                        _forceFlush = false;
                        return;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (IsAsciiDigit((byte)b) || b == 0x3A || b == 0x3B)
                    {
                        _state = ParserState.CsiParametric(
                            _state.Semicolons, _state.Segments, _state.HasDigit, _state.FirstParamValue);
                        continue;
                    }

                    if (CanCompleteDeferredParametricCsi(_state, (byte)b, _protocolContext))
                    {
                        _state = ParserState.CsiParametric(
                            _state.Semicolons, _state.Segments, _state.HasDigit, _state.FirstParamValue);
                        continue;
                    }

                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                    continue;
                }

                case ParserStateTag.CsiPrivateReply:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }

                        if (CanDeferPrivateReplyCsi(_protocolContext))
                        {
                            _state = ParserState.CsiPrivateReplyDeferred(
                                _state.Semicolons, _state.HasDigit, _state.SawDollar);
                            _pendingSinceMs = null;
                            _forceFlush = false;
                            return;
                        }

                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (IsAsciiDigit((byte)b))
                    {
                        _cursor++;
                        _state = ParserState.CsiPrivateReply(_state.Semicolons, true, _state.SawDollar);
                        continue;
                    }

                    if (b == 0x3B)
                    {
                        _cursor++;
                        _state = ParserState.CsiPrivateReply(_state.Semicolons + 1, false, false);
                        continue;
                    }

                    if (b == 0x24 && _state.HasDigit && !_state.SawDollar)
                    {
                        _cursor++;
                        _state = ParserState.CsiPrivateReply(_state.Semicolons, true, true);
                        continue;
                    }

                    if (b >= 0x40 && b <= 0x7E)
                    {
                        int end = _cursor + 1;
                        EmitKeyOrResponse(StdinResponseProtocol.Csi, DecodeUtf8(bytes[_unitStart..end]));
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    _state = ParserState.CsiState();
                    continue;
                }

                case ParserStateTag.CsiPrivateReplyDeferred:
                {
                    if (_cursor >= bytes.Length)
                    {
                        _pendingSinceMs = null;
                        _forceFlush = false;
                        return;
                    }

                    if (b == ESC)
                    {
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (IsAsciiDigit((byte)b) || b == 0x3B || b == 0x24)
                    {
                        _state = ParserState.CsiPrivateReply(_state.Semicolons, _state.HasDigit, _state.SawDollar);
                        continue;
                    }

                    if (CanCompleteDeferredPrivateReplyCsi(_state, (byte)b, _protocolContext))
                    {
                        _state = ParserState.CsiPrivateReply(_state.Semicolons, _state.HasDigit, _state.SawDollar);
                        continue;
                    }

                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                    continue;
                }

                case ParserStateTag.Osc:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (_state.SawEsc)
                    {
                        if (b == 0x5C)
                        {
                            int end = _cursor + 1;
                            EmitOpaqueResponse(StdinResponseProtocol.Osc, bytes[_unitStart..end]);
                            _state = ParserState.Ground();
                            ConsumePrefix(end);
                            continue;
                        }
                        _state = ParserState.OscState(false);
                        continue;
                    }

                    if (b == BEL)
                    {
                        int end = _cursor + 1;
                        EmitOpaqueResponse(StdinResponseProtocol.Osc, bytes[_unitStart..end]);
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    if (b == ESC)
                    {
                        _cursor++;
                        _state = ParserState.OscState(true);
                        continue;
                    }

                    _cursor++;
                    continue;
                }

                case ParserStateTag.Dcs:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (_state.SawEsc)
                    {
                        if (b == 0x5C)
                        {
                            int end = _cursor + 1;
                            EmitOpaqueResponse(StdinResponseProtocol.Dcs, bytes[_unitStart..end]);
                            _state = ParserState.Ground();
                            ConsumePrefix(end);
                            continue;
                        }
                        _state = ParserState.DcsState(false);
                        continue;
                    }

                    if (b == ESC)
                    {
                        _cursor++;
                        _state = ParserState.DcsState(true);
                        continue;
                    }

                    _cursor++;
                    continue;
                }

                case ParserStateTag.Apc:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if (_state.SawEsc)
                    {
                        if (b == 0x5C)
                        {
                            int end = _cursor + 1;
                            EmitOpaqueResponse(StdinResponseProtocol.Apc, bytes[_unitStart..end]);
                            _state = ParserState.Ground();
                            ConsumePrefix(end);
                            continue;
                        }
                        _state = ParserState.ApcState(false);
                        continue;
                    }

                    if (b == ESC)
                    {
                        _cursor++;
                        _state = ParserState.ApcState(true);
                        continue;
                    }

                    _cursor++;
                    continue;
                }

                case ParserStateTag.EscLessMouse:
                {
                    if (_cursor >= bytes.Length)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                        _state = ParserState.Ground();
                        ConsumePrefix(_cursor);
                        continue;
                    }

                    if ((b >= 0x30 && b <= 0x39) || b == 0x3B)
                    {
                        _cursor++;
                        continue;
                    }

                    if (b == 0x4D || b == 0x6D)
                    {
                        int end = _cursor + 1;
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart..end]);
                        _state = ParserState.Ground();
                        ConsumePrefix(end);
                        continue;
                    }

                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                    continue;
                }

                case ParserStateTag.EscLessX10Mouse:
                {
                    int end = _unitStart + 5;

                    if (bytes.Length < end)
                    {
                        if (!_forceFlush) { MarkPending(); return; }
                        EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart..bytes.Length]);
                        _state = ParserState.Ground();
                        ConsumePrefix(bytes.Length);
                        continue;
                    }

                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart..end]);
                    _state = ParserState.Ground();
                    ConsumePrefix(end);
                    continue;
                }

                default:
                    // Should never happen
                    _state = ParserState.Ground();
                    continue;
            }
        }
    }

    // ── Emit helpers ─────────────────────────────────────────────────────

    private void EmitKeyOrResponse(StdinResponseProtocol protocol, string raw)
    {
        var parsed = KeypressParser.Parse(raw, _useKittyKeyboard);
        if (parsed is not null)
        {
            _events.Enqueue(new StdinEvent.Key(parsed.Raw, parsed));
            return;
        }

        string protocolStr = protocol switch
        {
            StdinResponseProtocol.Csi => "csi",
            StdinResponseProtocol.Osc => "osc",
            StdinResponseProtocol.Dcs => "dcs",
            StdinResponseProtocol.Apc => "apc",
            _ => "unknown",
        };
        _events.Enqueue(new StdinEvent.Response(protocolStr, raw));
    }

    private void EmitMouse(ReadOnlySpan<byte> rawBytes, MouseEncoding encoding)
    {
        var evt = _mouseParser.ParseMouseEvent(rawBytes);
        if (evt is null)
        {
            EmitOpaqueResponse(StdinResponseProtocol.Unknown, rawBytes);
            return;
        }

        _events.Enqueue(new StdinEvent.Mouse(DecodeLatin1(rawBytes), encoding, evt));
    }

    private void EmitLegacyHighByte(byte b)
    {
        ReadOnlySpan<byte> single = [b];
        // The TS version uses Buffer.from([byte]) which creates a latin1/binary string
        // for parseKeypress. We pass the single byte as a latin1 string.
        string s = Encoding.Latin1.GetString(single);
        var parsed = KeypressParser.Parse(s, _useKittyKeyboard);
        if (parsed is not null)
        {
            _events.Enqueue(new StdinEvent.Key(parsed.Raw, parsed));
            return;
        }

        _events.Enqueue(new StdinEvent.Response("unknown", ((char)b).ToString()));
    }

    private void EmitOpaqueResponse(StdinResponseProtocol protocol, ReadOnlySpan<byte> rawBytes)
    {
        string protocolStr = protocol switch
        {
            StdinResponseProtocol.Csi => "csi",
            StdinResponseProtocol.Osc => "osc",
            StdinResponseProtocol.Dcs => "dcs",
            StdinResponseProtocol.Apc => "apc",
            _ => "unknown",
        };
        _events.Enqueue(new StdinEvent.Response(protocolStr, DecodeLatin1(rawBytes)));
    }

    // ── Buffer management ────────────────────────────────────────────────

    private void ConsumePrefix(int endExclusive)
    {
        _pending.Consume(endExclusive);
        _cursor = 0;
        _unitStart = 0;
        _pendingSinceMs = null;
        _forceFlush = false;
    }

    private byte[] TakePendingBytes()
    {
        var buffered = _pending.Take();
        _cursor = 0;
        _unitStart = 0;
        _pendingSinceMs = null;
        _forceFlush = false;
        return buffered;
    }

    private void FlushPendingOverflow()
    {
        if (_pending.Length == 0) return;

        EmitOpaqueResponse(StdinResponseProtocol.Unknown, _pending.View());
        _pending.Clear();
        _cursor = 0;
        _unitStart = 0;
        _pendingSinceMs = null;
        _forceFlush = false;
        _state = ParserState.Ground();
    }

    private void MarkPending()
    {
        _pendingSinceMs = Environment.TickCount64;
    }

    // ── Paste handling ───────────────────────────────────────────────────

    private Span<byte> ConsumePasteBytes(ReadOnlySpan<byte> chunk)
    {
        var paste = _paste!;
        byte[] combined = ConcatBytes(paste.Tail, chunk);
        int endIndex = IndexOfBytes(combined, BracketedPasteEnd);

        if (endIndex != -1)
        {
            PushPasteBytes(combined.AsSpan(0, endIndex));
            _events.Enqueue(new StdinEvent.Paste(JoinPasteBytes(paste.Parts, paste.TotalLength)));
            _paste = null;
            return combined.AsSpan(endIndex + BracketedPasteEnd.Length).ToArray();
        }

        int keep = Math.Min(BracketedPasteEnd.Length - 1, combined.Length);
        int stableLength = combined.Length - keep;
        if (stableLength > 0)
        {
            PushPasteBytes(combined.AsSpan(0, stableLength));
        }
        paste.Tail = combined.AsSpan(stableLength).ToArray();
        return [];
    }

    private void PushPasteBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return;
        _paste!.Parts.Add(bytes.ToArray());
        _paste!.TotalLength += bytes.Length;
    }

    // ── Deferred state reconciliation ────────────────────────────────────

    private void ReconcileDeferredStateWithProtocolContext()
    {
        switch (_state.Tag)
        {
            case ParserStateTag.CsiParametricDeferred:
                if (!CanDeferParametricCsi(_state, _protocolContext))
                {
                    var bytes = _pending.View();
                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                }
                return;

            case ParserStateTag.CsiPrivateReplyDeferred:
                if (!CanDeferPrivateReplyCsi(_protocolContext))
                {
                    var bytes = _pending.View();
                    EmitOpaqueResponse(StdinResponseProtocol.Unknown, bytes[_unitStart.._cursor]);
                    _state = ParserState.Ground();
                    ConsumePrefix(_cursor);
                }
                return;
        }
    }

    // ── Timeout management ───────────────────────────────────────────────

    private void TryForceFlush()
    {
        if (_paste is not null || _pendingSinceMs is null || _pending.Length == 0)
            return;
        _forceFlush = true;
    }

    private void ReconcileTimeoutState()
    {
        // In the C# port we don't arm actual timers internally — callers are
        // responsible for calling FlushTimeout() after the timeout period.
        // This matches the TS armTimeouts=false path. If armTimeouts is true,
        // we still set _pendingSinceMs so FlushTimeout() can decide.
        //
        // The TS version arms a setTimeout; in C# we leave that to the caller.
        // The _onTimeoutFlush callback can be invoked by the caller after
        // calling FlushTimeout() + Read()/Drain().
    }

    private void ResetState()
    {
        _pending.Reset(InitialPendingCapacity);
        _events.Clear();
        _pendingSinceMs = null;
        _forceFlush = false;
        _justFlushedEsc = false;
        _state = ParserState.Ground();
        _cursor = 0;
        _unitStart = 0;
        _paste = null;
        _mouseParser.Reset();
    }
}
