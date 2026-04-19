using System;
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { Testing = true, Width = 120, Height = 30 });

BoxRenderable mainContainer = new(renderer, new BoxOptions { Id = "mainContainer", Width = DimensionValue.Percent(100), Height = DimensionValue.Percent(100), FlexGrow = 1, MaxWidth = DimensionValue.Percent(100), MaxHeight = DimensionValue.Percent(100), BackgroundColor = Rgba.FromHex("#0f0f23"), FlexDirection = FlexDirectionValue.Column });
renderer.Root.Add(mainContainer);

BoxRenderable contentBox = new(renderer, new BoxOptions { Id = "content-box", FlexGrow = 1, BackgroundColor = Rgba.FromHex("#1e1e2e"), Border = true, BorderColor = Rgba.FromHex("#565f89"), Padding = DimensionValue.Point(1) });
ScrollBoxRenderable textBox = new(renderer, new ScrollBoxOptions { Id = "text-box", Position = PositionValue.Absolute, Left = DimensionValue.Point(2), Top = DimensionValue.Point(2), Width = DimensionValue.Point(80), Height = DimensionValue.Point(15), Border = true, BorderStyle = BorderStyle.Rounded, BorderColor = Rgba.FromHex("#9ece6a"), BackgroundColor = Rgba.FromHex("#11111b") });
contentBox.Add(textBox);
TextRenderable textRenderable = new(renderer, new TextOptions { Id = "text-renderable", Fg = Rgba.FromHex("#c0caf5"), WrapMode = WrapMode.Word });
textBox.Add(textRenderable);
textRenderable.Content = new StyledText(TextChunk.Plain("Hello world this should wrap inside the box, not paint as a top-level block."));

BoxRenderable instructionsBox = new(renderer, new BoxOptions { Id = "instructions-box", Width = DimensionValue.Percent(100), FlexDirection = FlexDirectionValue.Column, BackgroundColor = Rgba.FromHex("#1e1e2e"), Border = true, BorderColor = Rgba.FromHex("#565f89"), Padding = DimensionValue.Point(1) });
TextRenderable instructionsText1 = new(renderer, new TextOptions { Id = "instructions-1", Content = "Line1" });
TextRenderable instructionsText2 = new(renderer, new TextOptions { Id = "instructions-2", Content = "Line2" });
instructionsBox.Add(instructionsText1);
instructionsBox.Add(instructionsText2);

mainContainer.Add(contentBox);
mainContainer.Add(instructionsBox);
renderer.RenderTestFrame();
Console.WriteLine($"contentBox screen={contentBox.ScreenX},{contentBox.ScreenY} size={contentBox.Width}x{contentBox.Height}");
Console.WriteLine($"textBox screen={textBox.ScreenX},{textBox.ScreenY} size={textBox.Width}x{textBox.Height}");
Console.WriteLine($"textRenderable screen={textRenderable.ScreenX},{textRenderable.ScreenY} size={textRenderable.Width}x{textRenderable.Height}");
Console.WriteLine($"instructionsBox screen={instructionsBox.ScreenX},{instructionsBox.ScreenY} size={instructionsBox.Width}x{instructionsBox.Height}");
Console.WriteLine($"instructionsText1 screen={instructionsText1.ScreenX},{instructionsText1.ScreenY} size={instructionsText1.Width}x{instructionsText1.Height}");
Console.WriteLine($"instructionsText2 screen={instructionsText2.ScreenX},{instructionsText2.ScreenY} size={instructionsText2.Width}x{instructionsText2.Height}");
