# LiveMarkdown

A lightweight Avalonia control for rendering Markdown in .NET 8 applications. LiveMarkdown focuses on high-quality visual rendering, streaming/incremental preview scenarios and consistent theming (including LaTeX formula color enforcement).

## Supported features

- Markdown parsing via Markdig with advanced extensions enabled:
  - Headings (H1–H6)
  - Paragraphs
  - Ordered and unordered lists (with nesting)
  - Task lists (? / ?)
  - Tables (pipe tables)
  - Blockquotes
  - Horizontal rules
  - Links and images
  - Emoji support
- Fenced code blocks with syntax highlighting using AvaloniaEdit (common languages supported such as C#, JavaScript, Python, XML)
- Inline code rendering (monospace)
- Mermaid diagrams: render `fenced` code block with language `mermaid` as an inline diagram
- LaTeX / math support via AvaloniaMath:
  - Inline math (`$...$`)
  - Block math (`$$...$$`)
  - FormulaBlock rendering for inline and block math
- Incremental / streaming rendering:
  - Detects simple appends to existing content and re-renders only the tail
  - Optimized for live preview and streaming scenarios
  - Temporary highlighting of the last incoming chunk during streaming
- Styling and theming:
  - CSS-like classes and styles in `Generic.axaml` (e.g. `.md-heading`, `.md-paragraph`, `.md-code-block`, `.md-quote`, `.md-list`, `.md-table`)
  - Control enforces its own Foreground on generated elements to ensure consistent colors across themes
- Public API surface (high level):
  - `Text` (string) — Markdown content to render
  - `ContentForeground` / alias `Foreground` (IBrush) — color for already-rendered content
  - `LiveContentForeground` / alias `LiveForeground` (IBrush) — color for the currently incoming chunk

## Quick usage (XAML)

```xml
xmlns:lm="clr-namespace:LiveMarkdown.Controls;assembly=LiveMarkdown"

<!-- Rendered content white, live incoming chunk purple -->
<lm:MarkdownView Text="{Binding MarkdownText}"
                 Foreground="White"
                 LiveForeground="#a94dc1" />

<!-- Live stream example using resources -->
<lm:MarkdownView Text="{Binding LiveMarkdownText}"
                 Foreground="{StaticResource BrushTextPrimary}"
                 LiveForeground="{StaticResource ButtonBorderBrush}" />
```

## Dependencies

- .NET 8 (net8.0)
- Avalonia UI 11.3.x
- Markdig
- AvaloniaEdit (code block rendering & highlighting)
- AvaloniaMath (LaTeX rendering)

Exact package versions are listed in `LiveMarkdown.csproj`.

## Build & integration

1. Install .NET 8 SDK.
2. `dotnet restore`
3. `dotnet build`
4. Add project reference or NuGet package to your Avalonia app.
5. Include control styles in App resources:

```xml
<StyleInclude Source="avares://LiveMarkdown/Themes/Generic.axaml" />
```

6. Use the control in XAML: `<lm:MarkdownView Text="{Binding MarkdownText}" />`

## License

LiveMarkdown is licensed under the Apache License 2.0. See the `LICENSE` file for details.
