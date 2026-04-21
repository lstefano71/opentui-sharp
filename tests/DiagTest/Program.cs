using System;
using System.Text;
using OpenTui.Core;
using OpenTui.Core.Managed;
using OpenTui.Core.Managed.Unicode;

// Test: CJK word wrap at 35 — check actual rendering
var tb = ManagedTextBuffer.Create(WidthMethod.Unicode);
string text =
    "前后端分离 - TypeScript逻辑 + Go TUI界面\n" +
    "组件化设计 - 基于tview的可复用组件\n" +
    "渐进式交互 - 逐步披露避免信息过载\n" +
    "智能上下文 - 基于项目状态动态生成问题\n" +
    "丰富的问题类型 - 支持6种不同的交互形式\n" +
    "完整的验证 - 实时输入验证和错误处理";
tb.SetText(text);

var view = ManagedTextBufferView.Create(tb, 0, 0);
view.WrapMode = WrapMode.Word;
view.SetWrapWidth(35);
view.UpdateVirtualLines();

var buf = ManagedBuffer.Create(40, 20);
buf.Clear(new Rgba(0, 0, 0, 1));
buf.DrawTextBufferView(view, 0, 0);

string result = buf.GetResolvedText();
Console.WriteLine("IsAsciiOnly bug impact check:");
Console.WriteLine($"  Contains '形式': {result.Contains("形式")}");
Console.WriteLine($"  Contains '完整的验证': {result.Contains("完整的验证")}");
Console.WriteLine($"  Contains '实时输入验证和错误处理': {result.Contains("实时输入验证和错误处理")}");
Console.WriteLine($"  Contains stray 'å': {result.Contains("å")}");

// Check VLine WidthCols for lines with IsAsciiOnly bug
var vl = view.GetVirtualLines();
for (int i = 0; i < vl.Length; i++) {
    string lineText = tb.GetLineText(vl[i].SourceLine);
    uint perRuneWidth = 0;
    foreach (var rune in lineText.EnumerateRunes())
        perRuneWidth += TextWidth.CharWidth(rune, 8);
    uint reported = vl[i].WidthCols;
    string marker = reported != perRuneWidth && vl[i].SourceColOffset == 0 ? " ← WRONG (IsAsciiOnly bug)" : "";
    Console.WriteLine($"  VLine[{i}]: SrcLine={vl[i].SourceLine} WidthCols={reported} (perRune={perRuneWidth}){marker}");
}

view.Dispose(); tb.Dispose(); buf.Dispose();
