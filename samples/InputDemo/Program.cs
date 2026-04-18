using OpenTui.Core;
using System.Text.RegularExpressions;

using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = false,
    TargetFps = 30,
});

renderer.Native.SetBackgroundColor(Rgba.FromHex("#001122"));

InputRenderable? nameInput = null;
InputRenderable? emailInput = null;
InputRenderable? passwordInput = null;
InputRenderable? commentInput = null;
TextRenderable? keyLegendDisplay = null;
TextRenderable? statusDisplay = null;

string lastActionText = "Welcome to InputRenderable demo! Use Tab to navigate between fields.";
Rgba lastActionColor = Rgba.FromHex("#FFCC00");
int activeInputIndex = 0;
Timer? actionResetTimer = null;

var inputElements = new List<InputRenderable>();

var parentContainer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "parent-container",
    ZIndex = 10,
});
renderer.Root.Add(parentContainer);

nameInput = CreateInput("name-input", 5, 2, 40, "Enter your name...", 50);
emailInput = CreateInput("email-input", 5, 6, 40, "Enter your email...", 100);
passwordInput = CreateInput("password-input", 5, 10, 40, "Enter password...", 50);
commentInput = CreateInput("comment-input", 5, 14, 60, "Enter a comment...", 200);

inputElements.AddRange([nameInput, emailInput, passwordInput, commentInput]);
foreach (var input in inputElements)
{
    parentContainer.Add(input);
}

keyLegendDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "key-legend",
    Width = DimensionValue.Point(50),
    Height = DimensionValue.Point(12),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(50),
    Top = DimensionValue.Point(2),
    ZIndex = 50,
    Fg = Rgba.FromHex("#AAAAAA"),
});
parentContainer.Add(keyLegendDisplay);

statusDisplay = new TextRenderable(renderer, new TextOptions
{
    Id = "status-display",
    Width = DimensionValue.Point(80),
    Height = DimensionValue.Point(18),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(5),
    Top = DimensionValue.Point(19),
    ZIndex = 50,
});
parentContainer.Add(statusDisplay);

foreach (var input in inputElements)
{
    input.On<string>(InputRenderable.Events.Input, value =>
    {
        lastActionText = $"{GetInputName(input)} input: \"{value}\"";
        lastActionColor = Rgba.FromHex("#00FFFF");
        UpdateDisplays();
    });

    input.On<string>(InputRenderable.Events.Change, value =>
    {
        lastActionText = $"*** {GetInputName(input)} CHANGED: \"{value}\" ***";
        lastActionColor = Rgba.FromHex("#FF00FF");
        UpdateDisplays();
        ScheduleActionReset(1000);
    });

    input.On<string>(InputRenderable.Events.Enter, value =>
    {
        string inputName = GetInputName(input);
        bool isValid = inputName switch
        {
            "Name" => ValidateName(value),
            "Email" => ValidateEmail(value),
            "Password" => ValidatePassword(value),
            _ => true,
        };

        lastActionText = $"*** {inputName} SUBMITTED: \"{value}\" {(isValid ? "(Valid)" : "(Invalid)")} ***";
        lastActionColor = isValid ? Rgba.FromHex("#00FF00") : Rgba.FromHex("#FF0000");
        UpdateDisplays();
        ScheduleActionReset(1500);
    });

    input.On(RenderableEventNames.Focused, () => UpdateDisplays());
    input.On(RenderableEventNames.Blurred, () => UpdateDisplays());
}

renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    if (key.Name == "tab")
    {
        NavigateToInput(key.Shift ? activeInputIndex - 1 : activeInputIndex + 1);
        return;
    }

    if (key.Ctrl && key.Name == "f")
    {
        var activeInput = GetActiveInput();
        if (activeInput?.Focused == true)
        {
            activeInput.Blur();
            lastActionText = $"Focus removed from {GetInputName(activeInput)} input";
        }
        else
        {
            activeInput?.Focus();
            lastActionText = $"{GetInputName(activeInput)} input focused";
        }

        lastActionColor = Rgba.FromHex("#FFCC00");
        UpdateDisplays();
        return;
    }

    if (key.Ctrl && key.Name == "c")
    {
        var activeInput = GetActiveInput();
        if (activeInput is not null)
        {
            activeInput.Value = "";
            lastActionText = $"{GetInputName(activeInput)} input cleared";
            lastActionColor = Rgba.FromHex("#FFAA00");
            UpdateDisplays();
        }
        return;
    }

    if (key.Ctrl && key.Name == "r")
    {
        ResetInputs();
    }
});

UpdateDisplays();
nameInput.Focus();
await Task.Delay(Timeout.Infinite);

InputRenderable CreateInput(string id, int left, int top, int width, string placeholder, int maxLength) =>
    new(renderer, new InputOptions
    {
        Id = id,
        Position = PositionValue.Absolute,
        Left = DimensionValue.Point(left),
        Top = DimensionValue.Point(top),
        Width = DimensionValue.Point(width),
        Height = DimensionValue.Point(3),
        ZIndex = 100,
        BackgroundColor = Rgba.FromHex("#001122"),
        TextColor = Rgba.White,
        Placeholder = placeholder,
        PlaceholderColor = Rgba.FromHex("#666666"),
        CursorColor = Rgba.FromHex("#FFFF00"),
        Value = "",
        MaxLength = maxLength,
    });

InputRenderable? GetActiveInput() =>
    activeInputIndex >= 0 && activeInputIndex < inputElements.Count
        ? inputElements[activeInputIndex]
        : null;

void NavigateToInput(int index)
{
    GetActiveInput()?.Blur();
    activeInputIndex = Math.Max(0, Math.Min(index, inputElements.Count - 1));
    var newActive = GetActiveInput();
    newActive?.Focus();
    lastActionText = $"Switched to {GetInputName(newActive)} input";
    lastActionColor = Rgba.FromHex("#FFCC00");
    UpdateDisplays();
}

void ResetInputs()
{
    nameInput!.Value = "";
    emailInput!.Value = "";
    passwordInput!.Value = "";
    commentInput!.Value = "";

    lastActionText = "All inputs reset to empty values";
    lastActionColor = Rgba.FromHex("#FF00FF");
    UpdateDisplays();
    ScheduleActionReset(1000);
}

void ScheduleActionReset(int delayMs)
{
    actionResetTimer?.Dispose();
    actionResetTimer = new Timer(_ =>
    {
        lastActionColor = Rgba.FromHex("#FFCC00");
        UpdateDisplays();
    }, null, delayMs, Timeout.Infinite);
}

void UpdateDisplays()
{
    if (keyLegendDisplay is null || statusDisplay is null || nameInput is null || emailInput is null || passwordInput is null || commentInput is null)
        return;

    var activeInput = GetActiveInput();
    string activeInputName = GetInputName(activeInput);

    keyLegendDisplay.Content = new StyledText(
        TextChunk.Styled("Key Controls:", fg: Rgba.White, attributes: TextAttributes.Bold),
        TextChunk.Plain("\nTab/Shift+Tab: Navigate between inputs"),
        TextChunk.Plain("\nLeft/Right: Move cursor within input"),
        TextChunk.Plain("\nHome/End: Move to start/end of input"),
        TextChunk.Plain("\nBackspace/Delete: Remove characters"),
        TextChunk.Plain("\nEnter: Submit current input"),
        TextChunk.Plain("\nCtrl+F: Toggle focus on active input"),
        TextChunk.Plain("\nCtrl+C: Clear active input"),
        TextChunk.Plain("\nCtrl+R: Reset all inputs to defaults"),
        TextChunk.Plain("\nType: Enter text in focused field"));

    statusDisplay.Content = BuildStatusText(
        activeInputName,
        nameInput.Value,
        emailInput.Value,
        passwordInput.Value,
        commentInput.Value,
        nameInput.Focused,
        emailInput.Focused,
        passwordInput.Focused,
        commentInput.Focused,
        lastActionText,
        lastActionColor);
}

static StyledText BuildStatusText(
    string activeInputName,
    string nameValue,
    string emailValue,
    string passwordValue,
    string commentValue,
    bool nameFocused,
    bool emailFocused,
    bool passwordFocused,
    bool commentFocused,
    string lastActionText,
    Rgba lastActionColor) => new(
        TextChunk.Styled("Input Values:", fg: Rgba.White, attributes: TextAttributes.Bold),
        TextChunk.Plain("\nName: \""),
        TextChunk.Plain(nameValue),
        TextChunk.Plain("\" ("),
        StatusChunk(nameFocused),
        TextChunk.Plain(")\nEmail: \""),
        TextChunk.Plain(emailValue),
        TextChunk.Plain("\" ("),
        StatusChunk(emailFocused),
        TextChunk.Plain(")\nPassword: \""),
        TextChunk.Plain(new string('*', passwordValue.Length)),
        TextChunk.Plain("\" ("),
        StatusChunk(passwordFocused),
        TextChunk.Plain(")\nComment: \""),
        TextChunk.Plain(commentValue),
        TextChunk.Plain("\" ("),
        StatusChunk(commentFocused),
        TextChunk.Plain(")\n\n"),
        TextChunk.Styled($"Active Input: {activeInputName}", fg: Rgba.FromHex("#FFAA00"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\n\n"),
        TextChunk.Styled("Validation:", fg: Rgba.FromHex("#CCCCCC"), attributes: TextAttributes.Bold),
        TextChunk.Plain("\nName: "),
        ValidationChunk(ValidateName(nameValue), "✓ Valid", "✗ Invalid (min 2 chars)"),
        TextChunk.Plain("\nEmail: "),
        ValidationChunk(ValidateEmail(emailValue), "✓ Valid", "✗ Invalid format"),
        TextChunk.Plain("\nPassword: "),
        ValidationChunk(ValidatePassword(passwordValue), "✓ Valid", "✗ Invalid (min 6 chars)"),
        TextChunk.Plain("\n\n"),
        TextChunk.Styled(lastActionText, fg: lastActionColor));

static TextChunk StatusChunk(bool focused) =>
    TextChunk.Styled(focused ? "FOCUSED" : "BLURRED", fg: focused ? Rgba.FromHex("#00FF00") : Rgba.FromHex("#FF0000"));

static TextChunk ValidationChunk(bool valid, string validText, string invalidText) =>
    TextChunk.Styled(valid ? validText : invalidText, fg: valid ? Rgba.FromHex("#00FF00") : Rgba.FromHex("#FF0000"));

static string GetInputName(InputRenderable? input) =>
    input?.Id switch
    {
        "name-input" => "Name",
        "email-input" => "Email",
        "password-input" => "Password",
        "comment-input" => "Comment",
        _ => "Unknown",
    };

static bool ValidateName(string value) => value.Length >= 2;

static bool ValidateEmail(string value) =>
    Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

static bool ValidatePassword(string value) => value.Length >= 6;
