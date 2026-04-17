using System.Collections.Immutable;

namespace OpenTui;

/// <summary>
/// Rich text composed of styled chunks. Can be implicitly created from a plain string.
/// </summary>
public sealed class StyledText
{
    /// <summary>The styled chunks that make up this text.</summary>
    public ImmutableArray<StyledChunk> Chunks { get; }

    /// <summary>Creates a StyledText from an immutable array of styled chunks.</summary>
    public StyledText(ImmutableArray<StyledChunk> chunks) => Chunks = chunks;

    /// <summary>Creates a StyledText from an array of styled chunks.</summary>
    public StyledText(params StyledChunk[] chunks) => Chunks = [..chunks];

    /// <summary>Creates a StyledText from a plain string with default styling.</summary>
    public static implicit operator StyledText(string text) => new(new StyledChunk(text));

    /// <summary>Gets the plain text content without styling.</summary>
    public string PlainText => string.Concat(Chunks.Select(c => c.Text));

    /// <summary>Gets the total character length.</summary>
    public int Length => Chunks.Sum(c => c.Text.Length);

    /// <inheritdoc/>
    public override string ToString() => PlainText;
}
