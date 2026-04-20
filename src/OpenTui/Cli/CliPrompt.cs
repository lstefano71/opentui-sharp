namespace OpenTui.Cli;

/// <summary>Interactive text prompt with optional default value and validation.</summary>
/// <typeparam name="T">The type to parse the input as.</typeparam>
public sealed class CliPrompt<T> where T : IParsable<T>
{
    private readonly string _prompt;
    private T? _defaultValue;
    private bool _hasDefault;
    private Func<T, bool>? _validator;
    private string? _validationMessage;

    /// <summary>
    /// Initializes a new instance of the CliPrompt class.
    /// </summary>
    /// <param name="prompt">The prompt.</param>
    public CliPrompt(string prompt) => _prompt = prompt;

    /// <summary>Sets the default value shown in brackets.</summary>
    public CliPrompt<T> SetDefault(T value) { _defaultValue = value; _hasDefault = true; return this; }

    /// <summary>Sets a validation function.</summary>
    public CliPrompt<T> Validate(Func<T, bool> validator, string? message = null)
    {
        _validator = validator;
        _validationMessage = message;
        return this;
    }

    /// <summary>Prompts the user and returns the parsed value. Uses default console.</summary>
    public T Prompt() => Prompt(AnsiConsole.Instance);

    /// <summary>Prompts the user and returns the parsed value.</summary>
    public T Prompt(ICliConsole console)
    {
        while (true)
        {
            string prompt = _hasDefault ? $"{_prompt} [{_defaultValue}]: " : $"{_prompt}: ";
            console.Write(new StyledText(new StyledChunk(prompt, Fg: Rgba.FromHex("#87CEEB"))));

            string? input = console.ReadLine();

            if (string.IsNullOrEmpty(input) && _hasDefault)
                return _defaultValue!;

            if (input is not null && T.TryParse(input, null, out T? result))
            {
                if (_validator is null || _validator(result))
                    return result;

                console.WriteLine(new StyledText(new StyledChunk(
                    _validationMessage ?? "Invalid input. Try again.",
                    Fg: Rgba.FromHex("#FF4444"))));
                continue;
            }

            console.WriteLine(new StyledText(new StyledChunk(
                $"Could not parse input as {typeof(T).Name}. Try again.",
                Fg: Rgba.FromHex("#FF4444"))));
        }
    }
}

/// <summary>Yes/no confirmation prompt.</summary>
public sealed class CliConfirm
{
    private readonly string _prompt;
    private bool _defaultValue;

    /// <summary>
    /// Initializes a new instance of the CliConfirm class.
    /// </summary>
    /// <param name="prompt">The prompt.</param>
    public CliConfirm(string prompt) => _prompt = prompt;

    /// <summary>Sets the default value (true=yes, false=no).</summary>
    public CliConfirm SetDefault(bool value) { _defaultValue = value; return this; }

    /// <summary>
    /// Performs prompt.
    /// </summary>
    /// <returns>true if prompt; otherwise, false.</returns>
    public bool Prompt() => Prompt(AnsiConsole.Instance);

    /// <summary>
    /// Performs prompt.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <returns>true if prompt; otherwise, false.</returns>
    public bool Prompt(ICliConsole console)
    {
        string hint = _defaultValue ? "[Y/n]" : "[y/N]";
        console.Write(new StyledText(new StyledChunk($"{_prompt} {hint}: ", Fg: Rgba.FromHex("#87CEEB"))));

        string? input = console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return _defaultValue;

        return input.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }
}
