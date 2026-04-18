using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace OpenTui.Core;

/// <summary>
/// Parses raw terminal escape sequences into <see cref="ParsedKey"/> values.
/// Faithful 1:1 port of the TypeScript <c>parseKeypress</c> function.
/// </summary>
public static partial class KeypressParser
{
    // ── Regexes (source-generated for AOT) ──────────────────────────────

    [GeneratedRegex(@"^(?:\x1b)([a-zA-Z0-9])$")]
    private static partial Regex MetaKeyCodeRe();

    [GeneratedRegex(@"^(?:\x1b+)(O|N|\[|\[\[)(?:(\d+)(?:;(\d+))?([~^$])|(?:1;)?(\d+)?([a-zA-Z]))")]
    private static partial Regex FnKeyRe();

    [GeneratedRegex(@"^\x1b\[27;(\d+);(\d+)~$")]
    private static partial Regex ModifyOtherKeysRe();

    // ── Mouse / terminal response filters ───────────────────────────────

    [GeneratedRegex(@"^\x1b\[<\d+;\d+;\d+[Mm]$")]
    private static partial Regex SgrMouseRe();

    [GeneratedRegex(@"^\[<\d+;\d+;\d+[Mm]$")]
    private static partial Regex SgrMouseNoEscRe();

    [GeneratedRegex(@"^\x1b\[<[\d;]*$")]
    private static partial Regex PartialSgrMouseRe();

    [GeneratedRegex(@"^\[<[\d;]*$")]
    private static partial Regex PartialSgrMouseNoEscRe();

    [GeneratedRegex(@"^\x1b\[\d+;\d+;\d+t$")]
    private static partial Regex WindowSizeReportRe();

    [GeneratedRegex(@"^\x1b\[\d+;\d+R$")]
    private static partial Regex CursorPositionReportRe();

    [GeneratedRegex(@"^\x1b\[\?[\d;]+c$")]
    private static partial Regex DeviceAttributesRe();

    [GeneratedRegex(@"^\x1b\[\?[\d;]+\$y$")]
    private static partial Regex ModeReportRe();

    [GeneratedRegex(@"^\x1b\][\d;].*(\x1b\\|\x07)$")]
    private static partial Regex OscResponseRe();

    [GeneratedRegex(@"^[A-Z]$")]
    private static partial Regex UpperCaseLetterRe();

    // ── Key-name dictionary ─────────────────────────────────────────────

    private static readonly FrozenDictionary<string, string> s_keyName = new Dictionary<string, string>
    {
        // xterm/gnome ESC O letter
        ["OP"] = "f1",
        ["OQ"] = "f2",
        ["OR"] = "f3",
        ["OS"] = "f4",
        // xterm/rxvt ESC [ number ~
        ["[11~"] = "f1",
        ["[12~"] = "f2",
        ["[13~"] = "f3",
        ["[14~"] = "f4",
        // from Cygwin and used in libuv
        ["[[A"] = "f1",
        ["[[B"] = "f2",
        ["[[C"] = "f3",
        ["[[D"] = "f4",
        ["[[E"] = "f5",
        // common
        ["[15~"] = "f5",
        ["[17~"] = "f6",
        ["[18~"] = "f7",
        ["[19~"] = "f8",
        ["[20~"] = "f9",
        ["[21~"] = "f10",
        ["[23~"] = "f11",
        ["[24~"] = "f12",
        ["[29~"] = "menu",
        ["[57427~"] = "clear",
        // xterm ESC [ letter
        ["[A"] = "up",
        ["[B"] = "down",
        ["[C"] = "right",
        ["[D"] = "left",
        ["[E"] = "clear",
        ["[F"] = "end",
        ["[H"] = "home",
        ["[P"] = "f1",
        ["[Q"] = "f2",
        ["[S"] = "f4",
        // xterm/gnome ESC O letter
        ["OA"] = "up",
        ["OB"] = "down",
        ["OC"] = "right",
        ["OD"] = "left",
        ["OE"] = "clear",
        ["OF"] = "end",
        ["OH"] = "home",
        // xterm/rxvt ESC [ number ~
        ["[1~"] = "home",
        ["[2~"] = "insert",
        ["[3~"] = "delete",
        ["[4~"] = "end",
        ["[5~"] = "pageup",
        ["[6~"] = "pagedown",
        // putty
        ["[[5~"] = "pageup",
        ["[[6~"] = "pagedown",
        // rxvt
        ["[7~"] = "home",
        ["[8~"] = "end",
        // rxvt keys with modifiers
        ["[a"] = "up",
        ["[b"] = "down",
        ["[c"] = "right",
        ["[d"] = "left",
        ["[e"] = "clear",
        // option + arrow keys (old style)
        ["f"] = "right",
        ["b"] = "left",
        ["p"] = "up",
        ["n"] = "down",

        ["[2$"] = "insert",
        ["[3$"] = "delete",
        ["[5$"] = "pageup",
        ["[6$"] = "pagedown",
        ["[7$"] = "home",
        ["[8$"] = "end",

        ["Oa"] = "up",
        ["Ob"] = "down",
        ["Oc"] = "right",
        ["Od"] = "left",
        ["Oe"] = "clear",

        ["[2^"] = "insert",
        ["[3^"] = "delete",
        ["[5^"] = "pageup",
        ["[6^"] = "pagedown",
        ["[7^"] = "home",
        ["[8^"] = "end",
        // misc.
        ["[Z"] = "tab",
    }.ToFrozenDictionary();

    // ── Shift / Ctrl code sets ──────────────────────────────────────────

    private static readonly FrozenSet<string> s_shiftCodes = new[]
    {
        "[a", "[b", "[c", "[d", "[e",
        "[2$", "[3$", "[5$", "[6$", "[7$", "[8$",
        "[Z",
    }.ToFrozenSet();

    private static readonly FrozenSet<string> s_ctrlCodes = new[]
    {
        "Oa", "Ob", "Oc", "Od", "Oe",
        "[2^", "[3^", "[5^", "[6^", "[7^", "[8^",
    }.ToFrozenSet();

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maps raw control bytes (0x00–0x1f) to the key name that bindings expect.
    /// Ctrl+A..Z → 0x01..0x1a, Ctrl+\..Ctrl+_ → 0x1c..0x1f.
    /// </summary>
    private static string? GetCtrlKeyName(int charCode)
    {
        if (charCode == 0)
            return "space";

        if (charCode >= 1 && charCode <= 26)
            return ((char)(charCode + 'a' - 1)).ToString();

        if (charCode >= 28 && charCode <= 31)
            return ((char)(charCode + 64)).ToString();

        return null;
    }

    /// <summary>Kitty keyboard protocol parser — TODO: port from parse.keypress-kitty.ts</summary>
    internal static ParsedKey? ParseKittyKeyboard(string s) => null;

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// Parse a single terminal input sequence into a <see cref="ParsedKey"/>.
    /// Returns <c>null</c> for mouse events, terminal responses, and other
    /// non-keyboard sequences.
    /// </summary>
    public static ParsedKey? Parse(string s, bool useKittyKeyboard = false)
    {
        s ??= "";

        // ── Filter out mouse events (SGR and basic) ─────────────────
        if (SgrMouseRe().IsMatch(s))
            return null;
        if (SgrMouseNoEscRe().IsMatch(s))
            return null;
        if (PartialSgrMouseRe().IsMatch(s))
            return null;
        if (PartialSgrMouseNoEscRe().IsMatch(s))
            return null;
        if (s.StartsWith("\x1b[M") && s.Length >= 6)
            return null;

        // ── Filter out terminal response sequences ──────────────────
        if (WindowSizeReportRe().IsMatch(s))
            return null;
        if (CursorPositionReportRe().IsMatch(s))
            return null;
        if (DeviceAttributesRe().IsMatch(s))
            return null;
        if (ModeReportRe().IsMatch(s))
            return null;
        // Focus events
        if (s == "\x1b[I" || s == "\x1b[O")
            return null;
        if (OscResponseRe().IsMatch(s))
            return null;
        // Bracketed paste mode markers
        if (s == "\x1b[200~" || s == "\x1b[201~")
            return null;

        // ── Build default key ───────────────────────────────────────
        var name = "";
        var ctrl = false;
        var meta = false;
        var shift = false;
        var option = false;
        var number = false;
        var sequence = s;
        string? code = null;
        var super_ = false;
        var hyper = false;

        string? ctrlKeyName = s.Length == 1 ? GetCtrlKeyName(s[0]) : null;
        string? metaCtrlKeyName = s.Length == 2 && s[0] == '\x1b'
            ? GetCtrlKeyName(s[1])
            : null;

        // ── Kitty keyboard protocol ─────────────────────────────────
        if (useKittyKeyboard)
        {
            var kittyResult = ParseKittyKeyboard(s);
            if (kittyResult is not null)
                return kittyResult;
        }

        // ── modifyOtherKeys sequences (CSI 27;modifier;code ~) ──────
        var mokMatch = ModifyOtherKeysRe().Match(s);
        if (mokMatch.Success)
        {
            var modifier = int.Parse(mokMatch.Groups[1].Value) - 1;
            var charCode = int.Parse(mokMatch.Groups[2].Value);

            ctrl = (modifier & 4) != 0;
            meta = (modifier & 2) != 0;
            shift = (modifier & 1) != 0;
            option = (modifier & 2) != 0;
            super_ = (modifier & 8) != 0;
            hyper = (modifier & 16) != 0;

            if (charCode == 13)
                name = "return";
            else if (charCode == 27)
                name = "escape";
            else if (charCode == 9)
                name = "tab";
            else if (charCode == 32)
                name = "space";
            else if (charCode == 127 || charCode == 8)
                name = "backspace";
            else
            {
                var ch = ((char)charCode).ToString();
                name = ch;
                sequence = ch;
                if (charCode >= 48 && charCode <= 57)
                    number = true;
            }

            return new ParsedKey
            {
                Name = name,
                Ctrl = ctrl,
                Meta = meta,
                Shift = shift,
                Option = option,
                Number = number,
                Sequence = sequence,
                Raw = s,
                EventType = KeyEventType.Press,
                Source = "raw",
                Code = code,
                Super = super_,
                Hyper = hyper,
            };
        }

        // ── Simple key matching ─────────────────────────────────────
        if (s == "\r" || s == "\x1b\r")
        {
            name = "return";
            meta = s.Length == 2;
        }
        else if (s == "\n" || s == "\x1b\n")
        {
            name = "linefeed";
            meta = s.Length == 2;
        }
        else if (s == "\t")
        {
            name = "tab";
        }
        else if (s == "\b" || s == "\x1b\b" || s == "\x7f" || s == "\x1b\x7f")
        {
            name = "backspace";
            meta = s[0] == '\x1b';
        }
        else if (s == "\x1b" || s == "\x1b\x1b")
        {
            name = "escape";
            meta = s.Length == 2;
        }
        else if (s == " " || s == "\x1b ")
        {
            name = "space";
            meta = s.Length == 2;
        }
        else if (ctrlKeyName is not null)
        {
            name = ctrlKeyName;
            ctrl = true;
        }
        else if (s.Length == 1 && s[0] >= '0' && s[0] <= '9')
        {
            name = s;
            number = true;
        }
        else if (s.Length == 1 && s[0] >= 'a' && s[0] <= 'z')
        {
            name = s;
        }
        else if (s.Length == 1 && s[0] >= 'A' && s[0] <= 'Z')
        {
            name = s.ToLowerInvariant();
            shift = true;
        }
        else if (s.Length == 1 || (s.Length == 2 && char.IsHighSurrogate(s[0])))
        {
            // Single character (including emoji / surrogate pairs above BMP)
            name = s;
        }
        else
        {
            Match metaMatch;
            Match fnMatch;

            if ((metaMatch = MetaKeyCodeRe().Match(s)).Success)
            {
                meta = true;
                var ch = metaMatch.Groups[1].Value;
                var isUpperCase = UpperCaseLetterRe().IsMatch(ch);

                if (ch == "F")
                    name = "right";
                else if (ch == "B")
                    name = "left";
                else if (isUpperCase)
                {
                    shift = true;
                    name = ch;
                }
                else
                    name = ch;
            }
            else if (metaCtrlKeyName is not null)
            {
                meta = true;
                ctrl = true;
                name = metaCtrlKeyName;
            }
            else if ((fnMatch = FnKeyRe().Match(s)).Success)
            {
                // Check for double-ESC (option / meta)
                if (s.Length >= 2 && s[0] == '\x1b' && s[1] == '\x1b')
                {
                    option = true;
                    meta = true;
                }

                // Reassemble the key code, leaving out leading ESCs, the
                // modifier bitflag, and any meaningless "1;" sequence.
                var codeParts = new[] { fnMatch.Groups[1].Value, fnMatch.Groups[2].Value, fnMatch.Groups[4].Value, fnMatch.Groups[6].Value };
                code = string.Concat(codeParts.Where(p => p.Length > 0));

                var modStr = fnMatch.Groups[3].Value;
                if (modStr.Length == 0)
                    modStr = fnMatch.Groups[5].Value;
                if (modStr.Length == 0)
                    modStr = "1";
                var modifier = int.Parse(modStr) - 1;

                ctrl = ctrl || (modifier & 4) != 0;
                meta = meta || (modifier & 2) != 0;
                shift = shift || (modifier & 1) != 0;
                option = option || (modifier & 2) != 0;
                super_ = (modifier & 8) != 0;
                hyper = (modifier & 16) != 0;

                if (s_keyName.TryGetValue(code, out var keyNameResult))
                {
                    name = keyNameResult;
                    shift = s_shiftCodes.Contains(code) || shift;
                    ctrl = s_ctrlCodes.Contains(code) || ctrl;
                }
                else
                {
                    name = "";
                    code = null;
                }
            }
            else if (s == "\x1b[3~")
            {
                name = "delete";
                meta = false;
                code = "[3~";
            }
        }

        return new ParsedKey
        {
            Name = name,
            Ctrl = ctrl,
            Meta = meta,
            Shift = shift,
            Option = option,
            Number = number,
            Sequence = sequence,
            Raw = s,
            EventType = KeyEventType.Press,
            Source = "raw",
            Code = code,
            Super = super_,
            Hyper = hyper,
        };
    }
}
