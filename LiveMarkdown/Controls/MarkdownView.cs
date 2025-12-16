using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents; // TextBlock.Inlines, Run, Span, LineBreak, InlineUIContainer
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaMath.Controls;
using LiveMarkdown.Controls.Mermaid;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using System.Threading;
using System.Threading.Tasks;
// aliases for readability
using MdContainerInline = Markdig.Syntax.Inlines.ContainerInline;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;


namespace LiveMarkdown.Controls
{
    public class MarkdownView : TemplatedControl
    {
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<MarkdownView, string?>(nameof(Text));

        private string? _lastText;

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private Panel? _contentHost;

        private static readonly MarkdownPipeline Pipeline =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()   // headings, lists, tables, etc.
                .UseEmojiAndSmiley()       // :smile: :) etc.
                .UsePipeTables()           // tables | a | b |
                .UseMathematics()          // MathBlock, MathInline
                .UseTaskLists()
                .Build();

        #region Lifecycle

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _contentHost = e.NameScope.Find<Panel>("PART_ContentHost");
            RenderMarkdown();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                RenderMarkdown();
            }
        }

        #endregion

        #region Markdown rendering (blocks)

        private void RenderMarkdown()
        {
            if (_contentHost is null)
                return;

            if (string.IsNullOrWhiteSpace(Text))
            {
                _contentHost.Children.Clear();
                _lastText = Text;
                return;
            }

            // Is this a pure append at the end?
            bool isAppend = _lastText is not null
                            && Text is not null
                            && Text.Length > _lastText.Length
                            && Text.StartsWith(_lastText, StringComparison.Ordinal);

            _lastText = Text;

            // Parse the new document
            // Normalize LaTeX delimiters \(...\) and \[...\] to Markdown-style $...$ and $$...$$
            var normalizedText = Text
                .Replace("\\[", "$$")
                .Replace("\\]", "$$")
                .Replace("\\(", "$")
                .Replace("\\)","$");

            var doc = Markdown.Parse(normalizedText, Pipeline);

            // Collect ONLY blocks that we actually render
            var blocks = new List<Block>();
            foreach (var block in doc)
            {
                if (block is LinkReferenceDefinitionGroup)
                    continue;

                blocks.Add(block);
            }

            // If nothing was rendered or this isn't a simple append — full render
            if (_contentHost.Children.Count == 0 || !isAppend)
            {
                _contentHost.Children.Clear();

                foreach (var block in blocks)
                {
                    RenderBlock(block, _contentHost, listDepth: 0);
                }

                return;
            }

            // INCREMENTAL MODE — try to touch only the tail

            int existing = _contentHost.Children.Count;
            int blockCount = blocks.Count;

            // If block count decreased or something odd — do a full render.
            if (blockCount == 0 || blockCount < existing)
            {
                _contentHost.Children.Clear();
                foreach (var block in blocks)
                    RenderBlock(block, _contentHost, listDepth: 0);
                return;
            }

            // We assume 1:1 mapping: child[i] corresponds to blocks[i]
            // Keep everything except the last existing block,
            // and re-render from it upwards.
            int startIndex = Math.Max(0, existing - 1);

            // Remove children from startIndex to the end
            for (int i = _contentHost.Children.Count - 1; i >= startIndex; i--)
                _contentHost.Children.RemoveAt(i);

            // Render new blocks from startIndex to end
            for (int i = startIndex; i < blockCount; i++)
            {
                RenderBlock(blocks[i], _contentHost, listDepth: 0);
            }
        }


        /// <summary>
        /// Main dispatcher for blocks.
        /// listDepth – nesting level for lists (for indentation).
        /// </summary>
        private void RenderBlock(Block block, Panel target, int listDepth = 0)
        {
            try
            {
                // First check special cases inheriting from CodeBlock
                if (block is MathBlock mathBlock)
                {
                    RenderMathBlock(mathBlock, target);
                    return;
                }

                switch (block)
                {
                    case HeadingBlock heading:
                        RenderHeading(heading, target);
                        break;

                    case ParagraphBlock paragraph:
                        RenderParagraph(paragraph, target);
                        break;

                    case CodeBlock codeBlock:
                        RenderCodeBlock(codeBlock, target);
                        break;

                    case QuoteBlock quote:
                        RenderQuoteBlock(quote, target);
                        break;

                    case ListBlock list:
                        RenderListBlock(list, target, listDepth);
                        break;

                    case ThematicBreakBlock hr:
                        RenderThematicBreak(hr, target);
                        break;

                    case MdTable table:
                        RenderTable(table, target);
                        break;

                    case HtmlBlock htmlBlock:
                        RenderHtmlBlock(htmlBlock, target);
                        break;

                    default:
                        RenderUnknownBlock(block, target);
                        break;
                }
            }
            catch (Exception ex)
            {
                RenderErrorBlock(block, target, ex);
            }
        }




        private void RenderHtmlBlock(HtmlBlock htmlBlock, Panel target)
        {
            // raw HTML — could attach WebView / custom parser in the future
            var raw = htmlBlock.Lines.ToString().Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return;

            target.Children.Add(new TextBlock
            {
                Text = raw,
                Foreground = Brushes.LightGray,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            });
        }


        private void RenderErrorBlock(Block block, Panel target, Exception ex)
        {
            target.Children.Add(new TextBlock
            {
                Text = "⚠️ Render error: " + ex.Message,
                Foreground = Brushes.OrangeRed,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            });
        }

        private void RenderUnknownBlock(Block block, Panel target)
        {
            var text = block.ToString()?.Trim() ?? "[unknown block]";

            target.Children.Add(
                new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.LightGray,
                    FontStyle = FontStyle.Italic,
                    Margin = new Thickness(0, 4, 0, 12)
                }
            );
        }

        private void RenderHeading(HeadingBlock heading, Panel target)
        {
            var text = GetInlineText(heading.Inline as MdContainerInline);

            var tb = new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 12, 0, 4)
            };

            // classes for styling in XAML
            tb.Classes.Add("md-heading");
            tb.Classes.Add($"md-heading-{heading.Level}");

            target.Children.Add(tb);
        }

        private void RenderParagraph(ParagraphBlock paragraph, Panel target)
        {
            if (paragraph.Inline is not MdContainerInline container)
                return;

            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 12)
            };

            tb.Classes.Add("md-paragraph");
            tb.Classes.Add("md-anim-fadein");

            // Build inlines and highlight last word if this is the last paragraph
            bool highlightLastWord = false;
            if (_contentHost != null && _contentHost.Children.Count == 0) // full render, last block
                highlightLastWord = true;
            else if (_contentHost != null && _contentHost.Children.Count > 0 && target.Children.Count > 0 && ReferenceEquals(target.Children[target.Children.Count - 1], tb))
                highlightLastWord = true;

            BuildInlinesWithOptionalHighlight(container, tb.Inlines, highlightLastWord);

            target.Children.Add(tb);

            // Fade-in animation
            tb.Opacity = 0;
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(10);
                tb.Opacity = 1;
            });
        }

        private CancellationTokenSource? _highlightCts;

        private void BuildInlinesWithOptionalHighlight(MdContainerInline container, InlineCollection inlines, bool highlightLastWord)
        {
            MdInline? current = container.FirstChild;
            Run? lastRun = null;
            try
            {
                while (current is not null)
                {
                    switch (current)
                    {
                        case LiteralInline literal:
                            var text = literal.Content.ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                var words = text.Split(' ');
                                for (int i = 0; i < words.Length; i++)
                                {
                                    var word = words[i];
                                    if (string.IsNullOrEmpty(word)) continue;
                                    var run = new Run { Text = word };
                                    lastRun = run;
                                    inlines.Add(run);
                                    if (i < words.Length - 1)
                                        inlines.Add(new Run { Text = " " });
                                }
                            }
                            break;
                        case LineBreakInline:
                            inlines.Add(new LineBreak());
                            break;
                        case EmphasisInline emphasis:
                            AddEmphasisInline(emphasis, inlines);
                            break;
                        case CodeInline code:
                            inlines.Add(new Run
                            {
                                Text = code.Content,
                                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                                Background = Brushes.DimGray
                            });
                            break;
                        case LinkInline link:
                            AddLinkInline(link, inlines);
                            break;
                        case MathInline mathInline:
                            AddMathInline(mathInline, inlines);
                            break;
                        case TaskList task:
                            AddTaskListInline(task, inlines);
                            break;
                        case HtmlInline:
                            break;
                        default:
                            inlines.Add(new Run { Text = current.ToString() });
                            break;
                    }
                    current = current.NextSibling;
                }
            }
            catch
            {
                if (current is not null)
                    inlines.Add(new Run { Text = current.ToString() });
            }

            // Highlight last word if requested
            if (highlightLastWord && lastRun != null)
            {
                lastRun.Classes.Add("md-highlight");
                _highlightCts?.Cancel();
                _highlightCts = new CancellationTokenSource();
                var token = _highlightCts.Token;
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await Task.Delay(500, token);
                        lastRun.Classes.Remove("md-highlight");
                    }
                    catch { }
                });
            }
        }


        private void RenderCodeBlock(CodeBlock codeBlock, Panel target)
        {
            var codeText = codeBlock.Lines.ToString().TrimEnd('\r', '\n');

            // 1) Mermaid diagram
            if (codeBlock is FencedCodeBlock fenced)
            {
                var info = fenced.Info?.ToString()?.Trim().ToLowerInvariant();
                if (info == "mermaid" && !string.IsNullOrWhiteSpace(codeText))
                {
                    try
                    {
                        var mermaidControl = MermaidFactory.Create(codeText);
                        var viewer = new MermaidViewer(mermaidControl)
                        {
                            Margin = new Thickness(0, 8, 0, 12),
                            MaxHeight = 500, // ograniczenie wysokości widoku zintegrowanego
                            MinWidth = 700   // domyślna szerokość dla lepszej widoczności
                        };
                        target.Children.Add(viewer);
                        // Wyśrodkuj diagram po pełnym wyrenderowaniu
                        viewer.RequestFitToView();
                    }
                    catch (Exception ex)
                    {
                        target.Children.Add(new TextBlock
                        {
                            Text = $"[Błąd Mermaid]: {ex.Message}",
                            Foreground = Brushes.OrangeRed,
                            Margin = new Thickness(0, 4, 0, 12)
                        });
                    }
                    return;
                }
            }

            // 2) Normal code block — AvaloniaEdit

            var editor = new TextEditor
            {
                Text = codeText,
                IsReadOnly = true,
                ShowLineNumbers = false,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(4, 2, 4, 2),

                // Hard-coded values so it still looks ok even without styles:
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = Brushes.White,
                Background = Brushes.Transparent
            };

            // KEY: class name matches XAML
            editor.Classes.Add("md-code-block");

            // syntax highlighting
            if (codeBlock is FencedCodeBlock fencedBlock)
            {
                var info = fencedBlock.Info?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(info))
                {
                    var lang = info.Split(' ')[0].ToLowerInvariant();
                    string? hlName = lang switch
                    {
                        "csharp" or "cs" => "C#",
                        "javascript" or "js" => "JavaScript",
                        "json" => "JavaScript",
                        "xml" or "xaml" => "XML",
                        "html" or "htm" => "HTML",
                        "python" or "py" => "Python",
                        "cpp" or "c++" => "C++",
                        _ => null
                    };

                    if (hlName is not null)
                    {
                        editor.SyntaxHighlighting =
                            HighlightingManager.Instance.GetDefinition(hlName);
                    }
                }
            }

            // 3) Border around code — also hard-coded values + class for styling

            var border = new Border
            {
                Child = editor,
                Margin = new Thickness(0, 4, 0, 12),

                // DEFAULT background, even if styles don't load:
                Background = new SolidColorBrush(Color.FromArgb(255, 64, 64, 64)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4)
            };

            border.Classes.Add("md-code-container");

            target.Children.Add(border);
        }


        private void RenderQuoteBlock(QuoteBlock quote, Panel target)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 4, 0, 12)
            };

            border.Classes.Add("md-quote");

            var innerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 4
            };

            foreach (var childBlock in quote)
            {
                RenderBlock(childBlock, innerPanel, listDepth: 0);
            }

            border.Child = innerPanel;
            target.Children.Add(border);
        }

        private void RenderListBlock(ListBlock list, Panel target, int listDepth)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(listDepth * 16, 4, 0, 12),
                Spacing = 2
            };

            panel.Classes.Add("md-list");

            int index = 1;

            foreach (var item in list)
            {
                if (item is not ListItemBlock listItem)
                    continue;

                bool hasAnyChildBlock = false;
                foreach (var _ in listItem)
                {
                    hasAnyChildBlock = true;
                    break;
                }
                if (!hasAnyChildBlock)
                    continue;

                var itemGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star)
                    }
                };

                var markerText = list.IsOrdered ? $"{index}." : "•";

                var markerBlock = new TextBlock
                {
                    Text = markerText,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                markerBlock.Classes.Add("md-list-marker");

                Grid.SetColumn(markerBlock, 0);
                itemGrid.Children.Add(markerBlock);

                var itemContentPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 2
                };

                foreach (var childBlock in listItem)
                {
                    if (childBlock is ListBlock nestedList)
                    {
                        RenderListBlock(nestedList, itemContentPanel, listDepth + 1);
                    }
                    else
                    {
                        RenderBlock(childBlock, itemContentPanel, listDepth);
                    }
                }

                Grid.SetColumn(itemContentPanel, 1);
                itemGrid.Children.Add(itemContentPanel);

                panel.Children.Add(itemGrid);

                index++;
            }

            target.Children.Add(panel);
        }

        private void RenderTable(MdTable table, Panel target)
        {
            int columnCount = table.ColumnDefinitions?.Count ?? 0;
            if (columnCount <= 0)
                columnCount = 1;

            var grid = new Grid
            {
                Margin = new Thickness(0, 4, 0, 12)
            };

            grid.Classes.Add("md-table");

            for (int i = 0; i < columnCount; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            int rowIndex = 0;

            foreach (MdTableRow row in table)
            {
                bool isHeader = rowIndex == 0;

                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                int colIndex = 0;

                foreach (MdTableCell cell in row)
                {
                    var cellPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Spacing = 2,
                        Margin = new Thickness(2, 1, 2, 1)
                    };

                    foreach (var block in cell)
                    {
                        RenderBlock(block, cellPanel, listDepth: 0);
                    }

                    var cellBorder = new Border
                    {
                        Padding = new Thickness(4),
                        Child = cellPanel
                    };

                    cellBorder.Classes.Add(isHeader ? "md-table-header" : "md-table-cell");
                    // Usunięto md-anim-slidein, nie przesuwaj ostatniego wiersza

                    Grid.SetRow(cellBorder, rowIndex);
                    Grid.SetColumn(cellBorder, colIndex);
                    grid.Children.Add(cellBorder);

                    colIndex++;
                }

                rowIndex++;
            }

            target.Children.Add(grid);
        }

        private void RenderThematicBreak(ThematicBreakBlock hr, Panel target)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 8, 0, 8)
            };

            border.Classes.Add("md-hr");

            target.Children.Add(border);
        }

        private void RenderMathBlock(MathBlock mathBlock, Panel target)
        {
            var latex = mathBlock.Lines.ToString().Trim();
            if (string.IsNullOrWhiteSpace(latex))
                return;

            try
            {
                var formula = new FormulaBlock
                {
                    Formula = latex,
                    FontSize = 24,
                    Margin = new Thickness(0, 8, 0, 12),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                formula.Classes.Add("md-math-block");

                target.Children.Add(formula);
            }
            catch
            {
                // Fallback: if LaTeX render fails, show as text
                target.Children.Add(new TextBlock
                {
                    Text = "$$ " + latex + " $$",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 12)
                });
            }
        }



        #endregion

        #region Inline rendering helpers

        private void BuildInlines(MdContainerInline container, InlineCollection inlines)
        {
            MdInline? current = container.FirstChild;

            try
            {
                while (current is not null)
                {
                    switch (current)
                    {
                        case LiteralInline literal:
                            AddLiteralWithMath(literal.Content.ToString(), inlines);
                            break;

                        case LineBreakInline:
                            inlines.Add(new LineBreak());
                            break;

                        case EmphasisInline emphasis:
                            AddEmphasisInline(emphasis, inlines);
                            break;

                        case CodeInline code:
                            inlines.Add(new Run
                            {
                                Text = code.Content,
                                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                                Background = Brushes.DimGray
                            });
                            break;

                        case LinkInline link:
                            AddLinkInline(link, inlines);
                            break;

                        case MathInline mathInline:
                            AddMathInline(mathInline, inlines);
                            break;

                        case TaskList task:
                            AddTaskListInline(task, inlines);
                            break;

                        case HtmlInline:
                            // do nothing — text is already in LiteralInline
                            break;

                        default:
                            inlines.Add(new Run { Text = current.ToString() });
                            break;
                    }

                    current = current.NextSibling;
                }
            }
            catch
            {
                if (current is not null)
                    inlines.Add(new Run { Text = current.ToString() });
            }
        }

        private void AddTaskListInline(TaskList task, InlineCollection inlines)
        {
            var checkbox = new Run
            {
                Text = task.Checked ? "☑ " : "☐ ",
                FontWeight = FontWeight.Bold
            };

            checkbox.Classes.Add("md-task-checkbox");

            inlines.Add(checkbox);
        }

        private void AddLiteralWithMath(string text, InlineCollection inlines)
        {
            if (string.IsNullOrEmpty(text))
                return;

            int pos = 0;
            while (pos < text.Length)
            {
                int start = text.IndexOf('$', pos);
                if (start == -1 || start == text.Length - 1)
                {
                    if (pos < text.Length)
                        inlines.Add(new Run { Text = text.Substring(pos) });
                    break;
                }

                if (start > pos)
                    inlines.Add(new Run { Text = text.Substring(pos, start - pos) });

                int end = text.IndexOf('$', start + 1);
                if (end == -1)
                {
                    inlines.Add(new Run { Text = text.Substring(start) });
                    break;
                }

                var latex = text.Substring(start + 1, end - start - 1);

                try
                {
                    var formula = new FormulaBlock
                    {
                        Formula = latex,
                        FontSize = 18
                    };

                    formula.Classes.Add("md-math-inline");

                    var containerInline = new InlineUIContainer
                    {
                        Child = formula
                    };

                    inlines.Add(containerInline);
                }
                catch
                {
                    inlines.Add(new Run { Text = $"${latex}$" });
                }

                pos = end + 1;
            }
        }


        private void AddEmphasisInline(EmphasisInline emphasis, InlineCollection inlines)
        {
            bool isBold = emphasis.DelimiterCount >= 2;

            if (emphasis is not MdContainerInline container)
                return;

            var span = new Span();

            MdInline? current = container.FirstChild;
            while (current is not null)
            {
                switch (current)
                {
                    case LiteralInline literal:
                        span.Inlines.Add(new Run
                        {
                            Text = literal.Content.ToString()
                        });
                        break;

                    case LineBreakInline:
                        span.Inlines.Add(new LineBreak());
                        break;

                    case CodeInline code:
                        span.Inlines.Add(new Run
                        {
                            Text = code.Content,
                            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                            Background = Brushes.DimGray
                        });
                        break;
                }

                current = current.NextSibling;
            }

            if (isBold)
                span.FontWeight = FontWeight.Bold;
            else
                span.FontStyle = FontStyle.Italic;

            span.Classes.Add("md-emphasis");

            inlines.Add(span);
        }

        private void AddMathInline(MathInline mathInline, InlineCollection inlines)
        {
            var latex = mathInline.Content.ToString().Trim();
            if (string.IsNullOrWhiteSpace(latex))
                return;

            try
            {
                var formula = new FormulaBlock
                {
                    Formula = latex,
                    FontSize = 18
                };

                formula.Classes.Add("md-math-inline");

                var container = new InlineUIContainer
                {
                    Child = formula
                };

                inlines.Add(container);
            }
            catch
            {
                inlines.Add(new Run
                {
                    Text = $"${latex}$"
                });
            }
        }


        private void AddLinkInline(LinkInline link, InlineCollection inlines)
        {
            string? url = link.GetDynamicUrl?.Invoke() ?? link.Url;

            var linkTextBuilder = new StringBuilder();
            if (link is MdContainerInline container)
            {
                MdInline? current = container.FirstChild;
                while (current is not null)
                {
                    if (current is LiteralInline literal)
                    {
                        linkTextBuilder.Append(literal.Content.ToString());
                    }
                    current = current.NextSibling;
                }
            }

            var text = linkTextBuilder.Length > 0
                ? linkTextBuilder.ToString()
                : (url ?? string.Empty);

            if (string.IsNullOrWhiteSpace(url))
            {
                inlines.Add(new Run { Text = text });
                return;
            }

            // Create TextBlock with class md-link-text (already used)
            var textBlock = new TextBlock
            {
                Text = text,
                Padding = new Thickness(0)
            };

            textBlock.Classes.Add("md-link-text");

            // Create a button and assign it the md-link class
            var button = new Button
            {
                Content = textBlock,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            // KEY CHANGE: use md-link (matching Generic.axaml)
            button.Classes.Add("md-link");

            // Accessibility: set a readable name for assistive tools
            button.SetValue(Avalonia.Automation.AutomationProperties.NameProperty, text);

            button.Click += (_, _) => OpenUrl(url);

            var inlineContainer = new InlineUIContainer
            {
                Child = button
            };

            inlines.Add(inlineContainer);
        }


        #endregion

        #region Utility helpers

        private static string GetInlineText(MdContainerInline? container)
        {
            if (container is null)
                return string.Empty;

            var sb = new StringBuilder();

            MdInline? current = container.FirstChild;
            while (current is not null)
            {
                switch (current)
                {
                    case LiteralInline literal:
                        sb.Append(literal.Content.ToString());
                        break;

                    case LineBreakInline:
                        sb.Append('\n');
                        break;

                    case EmphasisInline emphasis:
                        sb.Append(GetInlineText(emphasis as MdContainerInline));
                        break;

                    case CodeInline code:
                        sb.Append(code.Content);
                        break;

                    case LinkInline link:
                        sb.Append(GetInlineText(link as MdContainerInline));
                        break;
                }

                current = current.NextSibling;
            }

            return sb.ToString();
        }

        private static void OpenUrl(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch
            {
                // TODO: log / show toast
            }
        }

        #endregion
    }
}
