using OpenTui;
using OpenTui.Cli;

// Big title
new CliFiglet("OPENTUI").Write();
Console.WriteLine();

// Horizontal rule
new CliRule("CLI Widget Demo").Write();
Console.WriteLine();

// Panel
new CliPanel("OpenTUI-sharp provides inline CLI widgets\nthat work without the native renderer.")
    .SetTitle("About")
    .SetBorder(BorderStyle.Rounded)
    .Write();
Console.WriteLine();

// Table
new CliTable()
    .SetTitle("Widget Catalog")
    .SetBorder(BorderStyle.Rounded)
    .AddColumn("Widget", "Type", "Status")
    .AddRow("CliTable", "Static", "✅ Ready")
    .AddRow("CliPanel", "Static", "✅ Ready")
    .AddRow("CliRule", "Static", "✅ Ready")
    .AddRow("CliPrompt", "Interactive", "✅ Ready")
    .AddRow("CliSelect", "Interactive", "✅ Ready")
    .AddRow("CliProgress", "Animated", "✅ Ready")
    .AddRow("CliStatus", "Animated", "✅ Ready")
    .AddRow("CliFiglet", "Static", "✅ Ready")
    .Write();
Console.WriteLine();

// Another rule
new CliRule().SetChar('═').Write();
Console.WriteLine();

// Confirm prompt (non-interactive in CI, just show the API)
Console.WriteLine("Example API usage:");
Console.WriteLine("  var name = new CliPrompt<string>(\"What is your name?\").Prompt();");
Console.WriteLine("  var ok = new CliConfirm(\"Continue?\").Prompt();");
Console.WriteLine("  var choice = new CliSelect<string>(\"Pick one\")");
Console.WriteLine("      .AddChoices(\"Red\", \"Green\", \"Blue\").Prompt();");
