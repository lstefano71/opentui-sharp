// Simple Layout Example — demonstrates flexbox layouts with OpenTUI
// Port of simple-layout-example.ts
using OpenTui.Core;

// --- Create renderer ---
using var renderer = CliRenderer.Create(new CliRendererConfig
{
    ExitOnCtrlC = true,
    TargetFps = 30,
});

int currentDemoIndex = 0;
bool autoplayEnabled = true;
bool moveableVisible = true;
int moveX = 0, moveY = 0;
Timer? autoAdvanceTimer = null;

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    ZIndex = 0,
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#3b82f6"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    Border = true,
});

var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "LAYOUT DEMO",
    Fg = Rgba.FromInts(255, 255, 255),
    Bg = Rgba.Transparent,
    ZIndex = 1,
});
header.Add(headerText);

// --- Content area ---
var contentArea = new BoxRenderable(renderer, new BoxOptions
{
    Id = "content-area",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexDirection = FlexDirectionValue.Row,
    FlexGrow = 1,
    FlexShrink = 1,
});

// --- Sidebar ---
var sidebar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "sidebar",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    BackgroundColor = Rgba.FromHex("#64748b"),
    BorderStyle = BorderStyle.Single,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var sidebarText = new TextRenderable(renderer, new TextOptions
{
    Id = "sidebar-text", Content = "SIDEBAR",
    Fg = Rgba.FromInts(255, 255, 255), Bg = Rgba.Transparent, ZIndex = 1,
});
sidebar.Add(sidebarText);

// --- Main content ---
var mainContent = new BoxRenderable(renderer, new BoxOptions
{
    Id = "main-content",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    BackgroundColor = Rgba.FromHex("#919599"),
    BorderStyle = BorderStyle.Single,
    FlexGrow = 1, FlexShrink = 1,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var mainContentText = new TextRenderable(renderer, new TextOptions
{
    Id = "main-content-text", Content = "MAIN CONTENT",
    Fg = Rgba.FromHex("#1e293b"), Bg = Rgba.Transparent, ZIndex = 1,
});
mainContent.Add(mainContentText);

// --- Right sidebar ---
var rightSidebar = new BoxRenderable(renderer, new BoxOptions
{
    Id = "right-sidebar",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    BorderStyle = BorderStyle.Single,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var rightSidebarText = new TextRenderable(renderer, new TextOptions
{
    Id = "right-sidebar-text", Content = "RIGHT",
    Fg = Rgba.FromInts(255, 255, 255), Bg = Rgba.Transparent, ZIndex = 1,
});
rightSidebar.Add(rightSidebarText);

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    BorderStyle = BorderStyle.Single,
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text", Content = "",
    Fg = Rgba.FromInts(255, 255, 255), Bg = Rgba.Transparent, ZIndex = 1,
});
footer.Add(footerText);

// --- Moveable overlay ---
var moveable = new BoxRenderable(renderer, new BoxOptions
{
    Id = "moveable",
    ZIndex = 100,
    Width = DimensionValue.Point(8),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#ff6b6b"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#ff4757"),
    Position = PositionValue.Absolute,
    Left = DimensionValue.Point(0),
    Top = DimensionValue.Point(0),
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var moveableText = new TextRenderable(renderer, new TextOptions
{
    Id = "moveable-text", Content = "MOVE",
    Fg = Rgba.FromInts(255, 255, 255), Bg = Rgba.Transparent, ZIndex = 101,
});
moveable.Add(moveableText);

// --- Absolute bottom-right box ---
var absoluteBox = new BoxRenderable(renderer, new BoxOptions
{
    Id = "absolute-box",
    ZIndex = 150,
    Width = DimensionValue.Point(20),
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#22c55e"),
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#16a34a"),
    Position = PositionValue.Absolute,
    Bottom = DimensionValue.Point(1),
    Right = DimensionValue.Point(1),
    FlexDirection = FlexDirectionValue.Row,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var absoluteText = new TextRenderable(renderer, new TextOptions
{
    Id = "absolute-text", Content = "BOTTOM RIGHT",
    Fg = Rgba.FromInts(255, 255, 255), Bg = Rgba.Transparent, ZIndex = 151,
});
absoluteBox.Add(absoluteText);

// --- Build tree ---
contentArea.Add(sidebar);
contentArea.Add(mainContent);
contentArea.Add(rightSidebar);
rightSidebar.Visible = false;

renderer.Root.Add(header);
renderer.Root.Add(contentArea);
renderer.Root.Add(footer);
renderer.Root.Add(moveable);
renderer.Root.Add(absoluteBox);

// --- Layout helpers ---
void ResetElement(BoxRenderable el)
{
    el.FlexBasis = DimensionValue.Auto;
    el.FlexGrow = 0;
    el.FlexShrink = 0;
    el.WidthDimension = DimensionValue.Auto;
    el.HeightDimension = DimensionValue.Auto;
}

void SetupHorizontal()
{
    sidebar.Visible = true;
    mainContent.Visible = true;
    rightSidebar.Visible = false;
    ResetElement(sidebar);
    ResetElement(mainContent);

    contentArea.FlexDirection = FlexDirectionValue.Row;
    contentArea.AlignItems = AlignValue.Stretch;

    int sidebarWidth = Math.Max(15, renderer.Width / 5);
    sidebar.FlexBasis = DimensionValue.Point(sidebarWidth);
    sidebar.WidthDimension = DimensionValue.Point(sidebarWidth);
    sidebar.MinWidth = DimensionValue.Point(15);
    sidebarText.ContentText = "LEFT SIDEBAR";
    sidebar.BackgroundColor = Rgba.FromHex("#64748b");

    mainContent.FlexGrow = 1;
    mainContent.FlexShrink = 1;
    mainContent.MinWidth = DimensionValue.Point(20);
    mainContentText.ContentText = "MAIN CONTENT";
    mainContent.BackgroundColor = Rgba.FromHex("#eab308");
}

void SetupVertical()
{
    sidebar.Visible = true;
    mainContent.Visible = true;
    rightSidebar.Visible = false;
    ResetElement(sidebar);
    ResetElement(mainContent);

    contentArea.FlexDirection = FlexDirectionValue.Column;
    contentArea.AlignItems = AlignValue.Stretch;

    int contentH = renderer.Height - 6;
    int topBarH = Math.Max(3, contentH / 5);
    sidebar.FlexBasis = DimensionValue.Point(topBarH);
    sidebar.HeightDimension = DimensionValue.Point(topBarH);
    sidebar.MinHeight = DimensionValue.Point(3);
    sidebarText.ContentText = "TOP BAR";
    sidebar.BackgroundColor = Rgba.FromHex("#059669");

    mainContent.FlexGrow = 1;
    mainContent.FlexShrink = 1;
    mainContent.MinHeight = DimensionValue.Point(5);
    mainContentText.ContentText = "MAIN CONTENT";
    mainContent.BackgroundColor = Rgba.FromHex("#eab308");
}

void SetupCentered()
{
    sidebar.Visible = false;
    mainContent.Visible = true;
    rightSidebar.Visible = false;
    ResetElement(mainContent);

    contentArea.FlexDirection = FlexDirectionValue.Row;
    contentArea.AlignItems = AlignValue.Stretch;
    contentArea.JustifyContent = JustifyValue.Center;

    int centerW = Math.Max(30, renderer.Width * 3 / 5);
    mainContent.FlexBasis = DimensionValue.Point(centerW);
    mainContent.WidthDimension = DimensionValue.Point(centerW);
    mainContent.MinWidth = DimensionValue.Point(30);
    mainContent.MaxWidth = DimensionValue.Point(renderer.Width * 4 / 5);
    mainContentText.ContentText = "CENTERED CONTENT";
    mainContent.BackgroundColor = Rgba.FromHex("#7c3aed");
}

void SetupThreeColumn()
{
    sidebar.Visible = true;
    mainContent.Visible = true;
    rightSidebar.Visible = true;
    ResetElement(sidebar);
    ResetElement(mainContent);
    ResetElement(rightSidebar);

    contentArea.FlexDirection = FlexDirectionValue.Row;
    contentArea.AlignItems = AlignValue.Stretch;

    int sw = Math.Max(12, renderer.Width * 15 / 100);
    sidebar.FlexBasis = DimensionValue.Point(sw);
    sidebar.WidthDimension = DimensionValue.Point(sw);
    sidebar.MinWidth = DimensionValue.Point(12);
    sidebarText.ContentText = "LEFT";
    sidebar.BackgroundColor = Rgba.FromHex("#dc2626");

    mainContent.FlexGrow = 1;
    mainContent.FlexShrink = 1;
    mainContent.MinWidth = DimensionValue.Point(20);
    mainContentText.ContentText = "CENTER";
    mainContent.BackgroundColor = Rgba.FromHex("#059669");

    rightSidebar.FlexBasis = DimensionValue.Point(sw);
    rightSidebar.WidthDimension = DimensionValue.Point(sw);
    rightSidebar.MinWidth = DimensionValue.Point(12);
    rightSidebarText.ContentText = "RIGHT";
    rightSidebar.BackgroundColor = Rgba.FromHex("#7c3aed");
}

string[] demoNames = ["Horizontal Layout", "Vertical Layout", "Centered Layout", "Three Column"];
Action[] demoSetups = [SetupHorizontal, SetupVertical, SetupCentered, SetupThreeColumn];

void CenterMoveable()
{
    moveX = (renderer.Width - 8) / 2;
    moveY = (renderer.Height - 3) / 2;
    moveable.Left = DimensionValue.Point(moveX);
    moveable.Top = DimensionValue.Point(moveY);
}

void UpdateFooter()
{
    string auto = autoplayEnabled ? "ON" : "OFF";
    string vis = moveableVisible ? "ON" : "OFF";
    footerText.ContentText = $"SPACE: next | R: restart | P: autoplay ({auto}) | V: overlay ({vis}) | WASD: move";
}

void ApplyDemo()
{
    string auto = autoplayEnabled ? "AUTO" : "MANUAL";
    headerText.ContentText = $"{demoNames[currentDemoIndex]} ({currentDemoIndex + 1}/{demoNames.Length}) - {auto}";
    demoSetups[currentDemoIndex]();

    autoAdvanceTimer?.Dispose();
    autoAdvanceTimer = null;
    if (autoplayEnabled)
    {
        autoAdvanceTimer = new Timer(_ =>
        {
            currentDemoIndex = (currentDemoIndex + 1) % demoNames.Length;
            ApplyDemo();
            renderer.RequestRender();
        }, null, 4000, Timeout.Infinite);
    }
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "space":
            currentDemoIndex = (currentDemoIndex + 1) % demoNames.Length;
            ApplyDemo();
            break;
        case "r":
            currentDemoIndex = 0;
            ApplyDemo();
            break;
        case "p":
            autoplayEnabled = !autoplayEnabled;
            ApplyDemo();
            UpdateFooter();
            break;
        case "v":
            moveableVisible = !moveableVisible;
            moveable.Visible = moveableVisible;
            UpdateFooter();
            break;
        case "w":
            moveY = Math.Max(0, moveY - 1);
            moveable.Top = DimensionValue.Point(moveY);
            break;
        case "a":
            moveX = Math.Max(0, moveX - 1);
            moveable.Left = DimensionValue.Point(moveX);
            break;
        case "s":
            moveY = Math.Min(renderer.Height - 3, moveY + 1);
            moveable.Top = DimensionValue.Point(moveY);
            break;
        case "d":
            moveX = Math.Min(renderer.Width - 8, moveX + 1);
            moveable.Left = DimensionValue.Point(moveX);
            break;
    }
});

renderer.On<(int Width, int Height)>(RendererEventNames.Resize, _ =>
{
    CenterMoveable();
});

// --- Start ---
renderer.Native.SetBackgroundColor(Rgba.FromHex("#001122"));
CenterMoveable();
UpdateFooter();
ApplyDemo();
renderer.RequestRender();

// Keep alive until Ctrl+C
await Task.Delay(Timeout.Infinite);
