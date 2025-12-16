# LiveMarkdown

LiveMarkdown is a lightweight Avalonia-based Markdown renderer and viewer for .NET 8. It focuses on producing a high-quality visual representation of Markdown documents (headings, lists, tables, code fences, inline and block LaTeX, task lists, quotes, horizontal rules and more) and includes features for incremental/stream rendering to support live preview scenarios.

Key features
- Full Markdown support via `Markdig` with advanced extensions enabled
- **Mermaid diagram support**: render Mermaid diagrams directly in Markdown using fenced code blocks with `mermaid` (see below)
- Inline and block math rendering using `AvaloniaMath` (LaTeX support)
- Syntax-highlighted fenced code blocks using `AvaloniaEdit`
- Tables, task lists, emojis, and more
- Incremental/stream rendering mode: the renderer is optimized to re-render only appended changes at the document end when used in streaming/live-edit scenarios, making it ideal for live preview while writing or piping content progressively
- Rich styling via `Generic.axaml` styles and CSS-like classes on visual elements

## Mermaid diagrams
LiveMarkdown now supports rendering [Mermaid](https://mermaid-js.github.io/) diagrams directly in your Markdown files. To use this feature, simply add a fenced code block with the language set to `mermaid`:

````markdown
```mermaid
graph TD;
    A-->B;
    A-->C;
    B-->D;
    C-->D;
```
````

The diagram will be rendered in place of the code block.

Dependencies
- .NET 8 (net8.0)
- Avalonia 11.3.9
- Avalonia.AvaloniaEdit 11.3.0
- AvaloniaMath 2.1.0
- Markdig 0.44.0

See `LiveMarkdown/LiveMarkdown.csproj` for exact package versions used in the project.

Build & usage
1. Install .NET 8 SDK: https://dotnet.microsoft.com/download
2. Clone the repository
3. Restore and build:
   - `dotnet restore`
   - `dotnet build`
4. Reference the library in your Avalonia project:
   - Add a project reference or install the NuGet package
   - Include the styles: `<StyleInclude Source="avares://LiveMarkdown/Themes/Generic.axaml" />`
   - Use in XAML: `<local:MarkdownView Text="{Binding MarkdownText}" />`

Notes about streaming and live preview
The renderer implements an incremental rendering mode that detects when the new input is a simple append to the previous content and attempts to update only the end of the visual tree. This reduces CPU and UI churn during typical live-edit workflows (typing or appending text) and makes the viewer suitable for streaming scenarios where Markdown arrives progressively.

Contributing
Contributions are welcome. Please open issues or pull requests. When contributing, try to keep changes small and focused and follow the existing code style.

License
This project is provided under the Apache License 2.0. See `LICENSE` for details.