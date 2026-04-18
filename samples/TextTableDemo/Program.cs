// TextTable Demo — demonstrates TextTableRenderable with data cycling, border styles, and column fitters
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true, TargetFps = 30 });

int dataSetIndex = 0;
int borderIndex = 0;
byte wrapMode = 2;
int fitterIndex = 0;

BorderStyle[] borderStyles = [BorderStyle.Single, BorderStyle.Double, BorderStyle.Rounded, BorderStyle.Heavy];
string[] borderNames = ["Single", "Double", "Rounded", "Heavy"];
string[] fitters = ["proportional", "balanced"];
string[] wrapNames = ["none", "char", "word"];

// --- Data sets ---
TextChunk[][][] BuildOperationsData()
{
    var ok = Rgba.FromHex("#22c55e");
    var err = Rgba.FromHex("#ef4444");
    var warn = Rgba.FromHex("#eab308");
    var hdr = Rgba.FromHex("#ffffff");
    return [
        [[TextChunk.Styled("Service", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Region", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Status", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Latency", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Queue", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Notes", hdr, attributes: TextAttributes.Bold)]],
        [[TextChunk.Plain("api-gateway")],
         [TextChunk.Plain("us-east-1")],
         [TextChunk.Styled("Healthy and stable", ok, attributes: TextAttributes.Bold)],
         [TextChunk.Plain("12 ms p95")],
         [TextChunk.Plain("18 queued")],
         [TextChunk.Plain("Serving public traffic and auth callbacks")]],
        [[TextChunk.Plain("auth-service")],
         [TextChunk.Plain("eu-west-1")],
         [TextChunk.Styled("Healthy and stable", ok, attributes: TextAttributes.Bold)],
         [TextChunk.Plain("8 ms p95")],
         [TextChunk.Plain("4 queued")],
         [TextChunk.Plain("Token minting and session refresh")]],
        [[TextChunk.Plain("db-primary")],
         [TextChunk.Plain("us-east-1")],
         [TextChunk.Styled("Degraded after failover", err, attributes: TextAttributes.Bold)],
         [TextChunk.Plain("timeout after 5 s")],
         [TextChunk.Plain("lag 12 s")],
         [TextChunk.Plain("Write traffic paused pending recovery")]],
        [[TextChunk.Plain("cache-redis")],
         [TextChunk.Plain("ap-southeast-1")],
         [TextChunk.Styled("Warning: eviction spikes", warn, attributes: TextAttributes.Bold)],
         [TextChunk.Plain("45 ms p95")],
         [TextChunk.Plain("2,304 keys/min")],
         [TextChunk.Plain("Hot shards rebalancing under peak load")]],
        [[TextChunk.Plain("msg-queue")],
         [TextChunk.Plain("us-west-2")],
         [TextChunk.Styled("Healthy and stable", ok, attributes: TextAttributes.Bold)],
         [TextChunk.Plain("3 ms p95")],
         [TextChunk.Plain("124 inflight")],
         [TextChunk.Plain("Background workers draining normally")]],
    ];
}

TextChunk[][][] BuildRegionalData()
{
    var hdr = Rgba.FromHex("#ffffff");
    var num = Rgba.FromHex("#7dd3fc");
    var pct = Rgba.FromHex("#86efac");
    return [
        [[TextChunk.Styled("Region", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Users", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Revenue", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Growth", hdr, attributes: TextAttributes.Bold)]],
        [[TextChunk.Plain("North America")], [TextChunk.Styled("1,245,000", num)], [TextChunk.Styled("$4.2M", num)], [TextChunk.Styled("+12.3%", pct)]],
        [[TextChunk.Plain("Europe")],        [TextChunk.Styled("892,000", num)],   [TextChunk.Styled("$2.8M", num)], [TextChunk.Styled("+8.7%", pct)]],
        [[TextChunk.Plain("Asia Pacific")],  [TextChunk.Styled("2,100,500", num)], [TextChunk.Styled("$5.1M", num)], [TextChunk.Styled("+22.1%", pct)]],
        [[TextChunk.Plain("Latin America")], [TextChunk.Styled("430,200", num)],   [TextChunk.Styled("$0.9M", num)], [TextChunk.Styled("+15.6%", pct)]],
    ];
}

TextChunk[][][] BuildTasksData()
{
    var hdr = Rgba.FromHex("#ffffff");
    return [
        [[TextChunk.Styled("Task", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Assignee", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Priority", hdr, attributes: TextAttributes.Bold)],
         [TextChunk.Styled("Status", hdr, attributes: TextAttributes.Bold)]],
        [[TextChunk.Plain("🐛 Fix login timeout")],     [TextChunk.Plain("Alice")],   [TextChunk.Styled("🔴 High", Rgba.FromHex("#ef4444"))],   [TextChunk.Plain("🔧 In Progress")]],
        [[TextChunk.Plain("✨ Add dark mode")],          [TextChunk.Plain("Bob")],     [TextChunk.Styled("🟡 Medium", Rgba.FromHex("#eab308"))], [TextChunk.Plain("📋 Backlog")]],
        [[TextChunk.Plain("🚀 Deploy v2.0")],           [TextChunk.Plain("Charlie")], [TextChunk.Styled("🔴 High", Rgba.FromHex("#ef4444"))],   [TextChunk.Plain("✅ Done")]],
        [[TextChunk.Plain("📝 Update API docs")],       [TextChunk.Plain("Diana")],   [TextChunk.Styled("🟢 Low", Rgba.FromHex("#22c55e"))],    [TextChunk.Plain("📋 Backlog")]],
        [[TextChunk.Plain("🔒 Security audit review")], [TextChunk.Plain("Eve")],     [TextChunk.Styled("🔴 High", Rgba.FromHex("#ef4444"))],   [TextChunk.Plain("🔧 In Progress")]],
    ];
}

string[] dataSetNames = ["Operations", "Regional", "Tasks"];
Func<TextChunk[][][]>[] dataSetBuilders = [BuildOperationsData, BuildRegionalData, BuildTasksData];

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e40af"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "TEXT TABLE DEMO",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- Table ---
var table = new TextTableRenderable(renderer, new TextTableOptions
{
    Id = "table",
    Content = BuildOperationsData(),
    WrapMode = wrapMode,
    ColumnWidthMode = "full",
    ColumnFitter = "proportional",
    ShowBorders = true,
    Border = true,
    OuterBorder = true,
    BorderStyle = BorderStyle.Single,
    BorderColor = Rgba.FromHex("#444444"),
    Fg = Rgba.FromHex("#cccccc"),
    Width = DimensionValue.Auto,
    FlexGrow = 1,
});

// --- Footer ---
var footer = new BoxRenderable(renderer, new BoxOptions
{
    Id = "footer",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#1e293b"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var footerText = new TextRenderable(renderer, new TextOptions
{
    Id = "footer-text",
    Content = "",
    Fg = Rgba.FromHex("#94a3b8"),
});
footer.Add(footerText);

// --- Build tree ---
renderer.Root.Add(header);
renderer.Root.Add(table);
renderer.Root.Add(footer);

void UpdateDisplay()
{
    headerText.ContentText = $"TEXT TABLE DEMO — {dataSetNames[dataSetIndex]} ({dataSetIndex + 1}/{dataSetNames.Length})";
    footerText.ContentText = $"[N] Data ({dataSetNames[dataSetIndex]})  [B] Border ({borderNames[borderIndex]})  [W] Wrap ({wrapNames[wrapMode]})  [F] Fitter ({fitters[fitterIndex]})";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "n":
            dataSetIndex = (dataSetIndex + 1) % dataSetNames.Length;
            table.Content = dataSetBuilders[dataSetIndex]();
            break;
        case "b":
            borderIndex = (borderIndex + 1) % borderStyles.Length;
            table.TableBorderStyle = borderStyles[borderIndex];
            break;
        case "w":
            wrapMode = (byte)((wrapMode + 1) % 3);
            table.WrapMode = wrapMode;
            break;
        case "f":
            fitterIndex = (fitterIndex + 1) % fitters.Length;
            table.ColumnFitter = fitters[fitterIndex];
            break;
    }
    UpdateDisplay();
});

UpdateDisplay();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
