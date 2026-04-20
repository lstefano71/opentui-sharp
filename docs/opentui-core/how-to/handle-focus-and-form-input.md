# How to: handle focus and form input

`InputRenderable` and `TextareaRenderable` give you editor-backed inputs with renderer-integrated focus, cursor, and selection behavior.

## Create inputs

```csharp
var nameInput = new InputRenderable(renderer, new InputOptions
{
    Id = "name",
    Width = 40,
    Placeholder = "Enter your name...",
});

var notesInput = new TextareaRenderable(renderer, new TextareaOptions
{
    Id = "notes",
    Width = 60,
    Height = 8,
    Placeholder = "Multi-line notes...",
});
```

## Move focus explicitly

```csharp
nameInput.Focus();
notesInput.Blur();
```

At renderer level you can also use `FocusRenderable(...)` and `BlurRenderable(...)`.

## Listen for input events

```csharp
nameInput.On<string>(InputRenderable.Events.Input, value => { /* live change */ });
nameInput.On<string>(InputRenderable.Events.Change, value => { /* committed change */ });
nameInput.On<string>(InputRenderable.Events.Enter, value => { /* submit */ });
notesInput.On(TextareaRenderable.Events.Submit, () => { /* submit */ });
```

## Typical key-handling pattern

Use renderer-level key input to move focus across controls:

```csharp
renderer.KeyInput.On("keypress", (KeyEvent key) =>
{
    if (key.Name == "tab")
    {
        nextInput.Focus();
        currentInput.Blur();
        key.StopPropagation();
    }
});
```

## Sample

For a complete example, see `samples\InputDemo`.
