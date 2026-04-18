// Code Demo — demonstrates CodeRenderable with syntax highlighting, scrolling, and wrap modes
using OpenTui.Core;

using var renderer = CliRenderer.Create(new CliRendererConfig { ExitOnCtrlC = true, TargetFps = 30 });

int sampleIndex = 0;
int wrapIndex = 0;
WrapMode[] wrapModes = [WrapMode.None, WrapMode.Char, WrapMode.Word];
string[] wrapNames = ["none", "char", "word"];

// --- Code samples ---
var samples = new (string Name, string Filetype, string Code)[]
{
    ("TypeScript", "typescript", """
        import { EventEmitter } from "events";

        interface User {
          id: string;
          name: string;
          email: string;
          roles: string[];
        }

        class UserService extends EventEmitter {
          private users: Map<string, User> = new Map();

          async getUser(id: string): Promise<User | undefined> {
            const cached = this.users.get(id);
            if (cached) return cached;

            const user = await this.fetchFromApi(id);
            if (user) {
              this.users.set(id, user);
              this.emit("user:loaded", user);
            }
            return user;
          }

          private async fetchFromApi(id: string): Promise<User | undefined> {
            const response = await fetch(`/api/users/${id}`);
            if (!response.ok) return undefined;
            return response.json() as Promise<User>;
          }
        }
        """),
    ("Python", "python", """
        from dataclasses import dataclass, field
        from typing import Optional
        import asyncio

        @dataclass
        class Config:
            host: str = "localhost"
            port: int = 8080
            debug: bool = False
            tags: list[str] = field(default_factory=list)

        async def start_server(config: Config) -> None:
            print(f"Starting server on {config.host}:{config.port}")
            reader, writer = await asyncio.open_connection(
                config.host, config.port
            )
            try:
                while data := await reader.read(1024):
                    message = data.decode()
                    print(f"Received: {message!r}")
                    writer.write(data)
                    await writer.drain()
            finally:
                writer.close()
                await writer.wait_closed()

        if __name__ == "__main__":
            asyncio.run(start_server(Config(debug=True)))
        """),
    ("Rust", "rust", """
        use std::collections::HashMap;
        use std::sync::Arc;
        use tokio::sync::RwLock;

        #[derive(Debug, Clone)]
        struct CacheEntry<T: Clone> {
            value: T,
            expires_at: std::time::Instant,
        }

        struct Cache<T: Clone + Send + Sync> {
            store: Arc<RwLock<HashMap<String, CacheEntry<T>>>>,
            ttl: std::time::Duration,
        }

        impl<T: Clone + Send + Sync> Cache<T> {
            fn new(ttl: std::time::Duration) -> Self {
                Self {
                    store: Arc::new(RwLock::new(HashMap::new())),
                    ttl,
                }
            }

            async fn get(&self, key: &str) -> Option<T> {
                let store = self.store.read().await;
                store.get(key).and_then(|entry| {
                    if entry.expires_at > std::time::Instant::now() {
                        Some(entry.value.clone())
                    } else {
                        None
                    }
                })
            }
        }
        """),
    ("C#", "csharp", """
        using System.Text.Json;
        using System.Collections.Concurrent;

        public sealed class MessageBus : IAsyncDisposable
        {
            private readonly ConcurrentDictionary<string, List<Func<JsonElement, Task>>> _handlers = new();
            private readonly Channel<(string Topic, JsonElement Payload)> _channel;

            public MessageBus(int capacity = 1000)
            {
                _channel = Channel.CreateBounded<(string, JsonElement)>(capacity);
                _ = ProcessMessages();
            }

            public void Subscribe(string topic, Func<JsonElement, Task> handler)
            {
                _handlers.AddOrUpdate(topic,
                    _ => [handler],
                    (_, list) => { list.Add(handler); return list; });
            }

            public async ValueTask PublishAsync(string topic, JsonElement payload)
            {
                await _channel.Writer.WriteAsync((topic, payload));
            }

            private async Task ProcessMessages()
            {
                await foreach (var (topic, payload) in _channel.Reader.ReadAllAsync())
                {
                    if (_handlers.TryGetValue(topic, out var handlers))
                    {
                        var tasks = handlers.Select(h => h(payload));
                        await Task.WhenAll(tasks);
                    }
                }
            }

            public async ValueTask DisposeAsync()
            {
                _channel.Writer.Complete();
                await _channel.Reader.Completion;
            }
        }
        """),
};

// --- Header ---
var header = new BoxRenderable(renderer, new BoxOptions
{
    Id = "header",
    Width = DimensionValue.Auto,
    Height = DimensionValue.Point(3),
    BackgroundColor = Rgba.FromHex("#7c3aed"),
    BorderStyle = BorderStyle.Single,
    AlignItems = AlignValue.Center,
    JustifyContent = JustifyValue.Center,
    Border = true,
});
var headerText = new TextRenderable(renderer, new TextOptions
{
    Id = "header-text",
    Content = "CODE DEMO",
    Fg = Rgba.FromInts(255, 255, 255),
});
header.Add(headerText);

// --- ScrollBox with Code ---
var scrollBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
{
    Id = "scroll",
    ScrollY = true,
    ScrollX = false,
    Width = DimensionValue.Auto,
    Height = DimensionValue.Auto,
    FlexGrow = 1,
    Border = true,
    BorderStyle = BorderStyle.Rounded,
    BorderColor = Rgba.FromHex("#444444"),
});

var code = new CodeRenderable(renderer, new CodeOptions
{
    Id = "code",
    Content = samples[0].Code,
    Filetype = samples[0].Filetype,
    Fg = Rgba.FromHex("#d4d4d4"),
    WrapMode = WrapMode.None,
    Width = DimensionValue.Auto,
    FlexGrow = 1,
});
scrollBox.Add(code);

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
renderer.Root.Add(scrollBox);
renderer.Root.Add(footer);

void UpdateDisplay()
{
    var (name, _, _) = samples[sampleIndex];
    headerText.ContentText = $"CODE DEMO — {name} ({sampleIndex + 1}/{samples.Length})";
    footerText.ContentText = $"[N] Language ({name})  [W] Wrap ({wrapNames[wrapIndex]})  [↑/↓/🖱] Scroll";
}

// --- Key handling ---
renderer.KeyInput.On("keypress", (KeyEvent e) =>
{
    switch (e.Name)
    {
        case "n":
            sampleIndex = (sampleIndex + 1) % samples.Length;
            code.Content = samples[sampleIndex].Code;
            code.Filetype = samples[sampleIndex].Filetype;
            break;
        case "w":
            wrapIndex = (wrapIndex + 1) % wrapModes.Length;
            code.WrapMode = wrapModes[wrapIndex];
            break;
        case "up":
            scrollBox.ScrollBy(0, -1);
            break;
        case "down":
            scrollBox.ScrollBy(0, 1);
            break;
        case "pageup":
            scrollBox.ScrollBy(0, -10);
            break;
        case "pagedown":
            scrollBox.ScrollBy(0, 10);
            break;
    }
    UpdateDisplay();
});

UpdateDisplay();
renderer.RequestRender();
await Task.Delay(Timeout.Infinite);
