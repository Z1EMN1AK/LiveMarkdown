// ===============================
// ATHENA – Native Mermaid Flowchart Renderer (C# / Avalonia)
// Extended version: Node shapes, ports, edge labels, edge styles, dynamic scaling, subgraph support
// ===============================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LiveMarkdown.Controls.Mermaid
{
    public enum NodePort { Left, Right, Top, Bottom }
    public enum NodeShape { Rectangle, RoundedRectangle, Diamond, Circle, Stadium, DoubleRectangle, Hexagon }
    public enum EdgeStyle { Solid, Dashed, Dotted, Bold, Thick }
    public enum EdgeArrowHead { None, Arrow, Open, Cross, Circle }

    public sealed class MermaidNode
    {
        public required string Id { get; init; } = string.Empty;
        public required string Text { get; init; } = string.Empty;
        public Point Position { get; set; }
        public Size Size { get; set; } = new Size(140, 50);
        public NodeShape Shape { get; set; } = NodeShape.Rectangle;
        public Color FillColor { get; set; } = Colors.White;
        public Color BorderColor { get; set; } = Colors.Black;
        public double BorderThickness { get; set; } = 2.0;
        public string? Subgraph { get; set; } // Optional grouping

        public Point GetPort(NodePort port)
        {
            // Returns the position of the port on the node's rectangle
            return port switch
            {
                NodePort.Left => new Point(Position.X, Position.Y + Size.Height / 2),
                NodePort.Right => new Point(Position.X + Size.Width, Position.Y + Size.Height / 2),
                NodePort.Top => new Point(Position.X + Size.Width / 2, Position.Y),
                NodePort.Bottom => new Point(Position.X + Size.Width / 2, Position.Y + Size.Height),
                _ => new Point(Position.X + Size.Width / 2, Position.Y + Size.Height / 2)
            };
        }
    }

    public sealed class MermaidEdge
    {
        public required MermaidNode From { get; init; }
        public required MermaidNode To { get; init; }
        public NodePort FromPort { get; set; }
        public NodePort ToPort { get; set; }
        public Vector FromPortOffset { get; set; } = new Vector(0, 0);
        public Vector ToPortOffset { get; set; } = new Vector(0, 0);
        public string Label { get; set; } = string.Empty;
        public Color StrokeColor { get; set; } = Colors.Black;
        public EdgeStyle Style { get; set; } = EdgeStyle.Solid;
        public EdgeArrowHead ArrowHead { get; set; } = EdgeArrowHead.Arrow;
    }

    public sealed class MermaidGraph
    {
        public Dictionary<string, MermaidNode> Nodes { get; } = new();
        public List<MermaidEdge> Edges { get; } = new();
        public bool LeftToRight { get; set; }
    }

    public static class MermaidParser
    {
        public static MermaidGraph Parse(string source)
        {
            var graph = new MermaidGraph();
            var lines = source.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentSubgraph = null;
            var nodeStyles = new Dictionary<string, Dictionary<string, string>>();
            var edgeStyles = new Dictionary<(string, string), Dictionary<string, string>>();
            var classDefs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var nodeClasses = new Dictionary<string, List<string>>();

            // Edge pattern definitions
            var edgePatterns = new (string pattern, EdgeStyle style, EdgeArrowHead arrow)[]
            {
                ("==>", EdgeStyle.Bold, EdgeArrowHead.Arrow),
                ("-->>", EdgeStyle.Solid, EdgeArrowHead.Arrow),
                ("--o", EdgeStyle.Solid, EdgeArrowHead.Circle),
                ("--x", EdgeStyle.Solid, EdgeArrowHead.Cross),
                ("---", EdgeStyle.Solid, EdgeArrowHead.None),
                ("-.->", EdgeStyle.Dashed, EdgeArrowHead.Arrow),
                ("-.o", EdgeStyle.Dashed, EdgeArrowHead.Circle),
                ("-.x", EdgeStyle.Dashed, EdgeArrowHead.Cross),
                ("..>", EdgeStyle.Dotted, EdgeArrowHead.Arrow),
                ("..o", EdgeStyle.Dotted, EdgeArrowHead.Circle),
                ("..x", EdgeStyle.Dotted, EdgeArrowHead.Cross),
                ("-->", EdgeStyle.Solid, EdgeArrowHead.Arrow),
            };

            bool inNoteBlock = false;
            var subgraphStack = new Stack<string?>();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Pomijaj bloki notatek :::note ... ::
                if (line.StartsWith(":::note")) { inNoteBlock = true; continue; }
                if (inNoteBlock)
                {
                    if (line.StartsWith(":::") && !line.StartsWith(":::note")) { inNoteBlock = false; }
                    continue;
                }

                // Komentarze Mermaid: pomijaj linie zaczynające się od %%
                if (line.StartsWith("%%"))
                    continue;

                if (line.StartsWith("flowchart"))
                {
                    graph.LeftToRight = line.Contains("LR");
                    continue;
                }

                if (line.StartsWith("subgraph"))
                {
                    var name = line.Length > 8 ? line.Substring(8).Trim() : null;
                    subgraphStack.Push(currentSubgraph);
                    currentSubgraph = name;
                    continue;
                }

                if (line.StartsWith("end"))
                {
                    currentSubgraph = subgraphStack.Count > 0 ? subgraphStack.Pop() : null;
                    continue;
                }

                if (line.StartsWith("classDef "))
                {
                    // classDef myClass fill:#f9f,stroke:#333,stroke-width:4px
                    var defParts = line.Substring(9).Split(' ', 2);
                    if (defParts.Length == 2)
                    {
                        var className = defParts[0].Trim();
                        var styleDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var styleItems = defParts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var item in styleItems)
                        {
                            var kv = item.Split(':', 2);
                            if (kv.Length == 2)
                                styleDict[kv[0].Trim()] = kv[1].Trim();
                        }
                        classDefs[className] = styleDict;
                    }
                    continue;
                }

                if (line.StartsWith("class "))
                {
                    // class A,B myClass
                    var classParts = line.Substring(6).Split(' ', 2);
                    if (classParts.Length == 2)
                    {
                        var ids = classParts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var className = classParts[1].Trim();
                        foreach (var id in ids)
                        {
                            if (!nodeClasses.TryGetValue(id, out var list))
                                nodeClasses[id] = list = new List<string>();
                            if (!list.Contains(className))
                                list.Add(className);
                        }
                    }
                    continue;
                }

                if (line.StartsWith("style "))
                {
                    // style A fill:#f9f,stroke:#333,stroke-width:4px
                    // style A--B stroke:#f00,stroke-width:3px
                    var styleParts = line.Substring(6).Split(' ', 2);
                    if (styleParts.Length == 2)
                    {
                        var id = styleParts[0].Trim();
                        var styleDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var styleItems = styleParts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var item in styleItems)
                        {
                            var kv = item.Split(':', 2);
                            if (kv.Length == 2)
                                styleDict[kv[0].Trim()] = kv[1].Trim();
                        }
                        if (id.Contains("--"))
                        {
                            // Edge style
                            var edgeIds = id.Split(new[] { "--" }, StringSplitOptions.None);
                            if (edgeIds.Length == 2)
                                edgeStyles[(edgeIds[0].Trim(), edgeIds[1].Trim())] = styleDict;
                        }
                        else
                        {
                            // Node style
                            nodeStyles[id] = styleDict;
                        }
                    }
                    continue;
                }

                // Obsługa etykiet na krawędzi: |label|
                string edgeLabel = string.Empty;
                string edgeLine = line;
                int labelStart = line.IndexOf('|');
                int labelEnd = line.IndexOf('|', labelStart + 1);
                if (labelStart >= 0 && labelEnd > labelStart)
                {
                    edgeLabel = line.Substring(labelStart + 1, labelEnd - labelStart - 1).Trim();
                    edgeLine = line.Remove(labelStart, labelEnd - labelStart + 1).Trim();
                }

                // Find matching edge pattern
                var match = edgePatterns.FirstOrDefault(p => edgeLine.Contains(p.pattern));
                if (string.IsNullOrEmpty(match.pattern)) continue;

                var parts = edgeLine.Split(match.pattern, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;

                var from = ParseNode(parts[0].Trim(), graph, currentSubgraph);
                var to = ParseNode(parts[1].Trim(), graph, currentSubgraph);
                if (from == null || to == null) continue;

                var edge = new MermaidEdge
                {
                    From = from,
                    To = to,
                    Label = edgeLabel,
                    Style = match.style,
                    ArrowHead = match.arrow
                };

                // Apply edge style if present
                if (edgeStyles.TryGetValue((from.Id, to.Id), out var estyle))
                {
                    if (estyle.TryGetValue("stroke", out var stroke))
                        edge.StrokeColor = ParseColor(stroke);
                    if (estyle.TryGetValue("stroke-width", out var strokeWidthStr) &&
                        double.TryParse(strokeWidthStr.Replace("px", ""), out var strokeWidth))
                        edge.Style = edge.Style == EdgeStyle.Bold || edge.Style == EdgeStyle.Thick ? edge.Style : EdgeStyle.Thick;
                }

                graph.Edges.Add(edge);
            }

            // Apply node styles and classes after all nodes are parsed
            foreach (var (nodeId, styleDict) in nodeStyles)
            {
                if (graph.Nodes.TryGetValue(nodeId, out var node))
                {
                    if (styleDict.TryGetValue("fill", out var fill))
                        node.FillColor = ParseColor(fill);
                    if (styleDict.TryGetValue("stroke", out var stroke))
                        node.BorderColor = ParseColor(stroke);
                    if (styleDict.TryGetValue("stroke-width", out var strokeWidthStr) &&
                        double.TryParse(strokeWidthStr.Replace("px", ""), out var strokeWidth))
                    {
                        node.BorderThickness = strokeWidth;
                    }
                }
            }
            // Apply classDef/class styles
            foreach (var (nodeId, classList) in nodeClasses)
            {
                if (graph.Nodes.TryGetValue(nodeId, out var node))
                {
                    foreach (var className in classList)
                    {
                        if (classDefs.TryGetValue(className, out var classStyle))
                        {
                            if (classStyle.TryGetValue("fill", out var fill))
                                node.FillColor = ParseColor(fill);
                            if (classStyle.TryGetValue("stroke", out var stroke))
                                node.BorderColor = ParseColor(stroke);
                            if (classStyle.TryGetValue("stroke-width", out var strokeWidthStr) &&
                                double.TryParse(strokeWidthStr.Replace("px", ""), out var strokeWidth))
                                node.BorderThickness = strokeWidth;
                        }
                    }
                }
            }

            return graph;
        }

        private static Color ParseColor(string value)
        {
            // Obsługa #rgb, #rrggbb, #aarrggbb, znanych nazw
            try
            {
                if (value.StartsWith("#"))
                {
                    if (value.Length == 4) // #rgb
                    {
                        var r = Convert.ToByte(new string(value[1], 2), 16);
                        var g = Convert.ToByte(new string(value[2], 2), 16);
                        var b = Convert.ToByte(new string(value[3], 2), 16);
                        return Color.FromRgb(r, g, b);
                    }
                    if (value.Length == 7) // #rrggbb
                    {
                        var r = Convert.ToByte(value.Substring(1, 2), 16);
                        var g = Convert.ToByte(value.Substring(3, 2), 16);
                        var b = Convert.ToByte(value.Substring(5, 2), 16);
                        return Color.FromRgb(r, g, b);
                    }
                    if (value.Length == 9) // #aarrggbb
                    {
                        var a = Convert.ToByte(value.Substring(1, 2), 16);
                        var r = Convert.ToByte(value.Substring(3, 2), 16);
                        var g = Convert.ToByte(value.Substring(5, 2), 16);
                        var b = Convert.ToByte(value.Substring(7, 2), 16);
                        return Color.FromArgb(a, r, g, b);
                    }
                }
                // Znane nazwy kolorów
                return (Color)typeof(Colors).GetProperty(value, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null)!;
            }
            catch { return Colors.Black; }
        }

        private static MermaidNode ParseNode(string token, MermaidGraph graph, string? subgraph)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Node token cannot be null or whitespace", nameof(token));

            string id = token;
            string text = token;
            NodeShape shape = NodeShape.Rectangle;

            // Alias: A[Start], B((End)), C{{Hex}} etc.
            // Extract ID (before first bracket/brace/paren), then parse shape/text
            int bracketIdx = token.IndexOfAny(new[] { '[', '(', '{', '<' });
            if (bracketIdx > 0)
            {
                id = token.Substring(0, bracketIdx).Trim();
                token = token.Substring(bracketIdx);
            }
            else
            {
                id = token.Trim();
            }

            // DoubleRectangle: [[text]]
            if (token.StartsWith("[[") && token.EndsWith("]]"))
            {
                text = token.Substring(2, token.Length - 4);
                shape = NodeShape.DoubleRectangle;
            }
            // Stadium: ([text])
            else if (token.StartsWith("([") && token.EndsWith(")]"))
            {
                text = token.Substring(2, token.Length - 4);
                shape = NodeShape.Stadium;
            }
            // Circle: (text)
            else if (token.StartsWith("(") && token.EndsWith(")"))
            {
                text = token.Substring(1, token.Length - 2);
                shape = NodeShape.Circle;
            }
            // Hexagon: {{text}}
            else if (token.StartsWith("{{") && token.EndsWith("}}"))
            {
                text = token.Substring(2, token.Length - 4);
                shape = NodeShape.Hexagon;
            }
            // RoundedRectangle: [(text)]
            else if (token.StartsWith("[(") && token.EndsWith(")]"))
            {
                text = token.Substring(2, token.Length - 4);
                shape = NodeShape.RoundedRectangle;
            }
            // Diamond: <text>
            else if (token.StartsWith("<") && token.EndsWith(">"))
            {
                text = token.Substring(1, token.Length - 2);
                shape = NodeShape.Diamond;
            }
            // Rectangle: [text]
            else if (token.StartsWith("[") && token.EndsWith("]"))
            {
                text = token.Substring(1, token.Length - 2);
                shape = NodeShape.Rectangle;
            }
            else
            {
                text = token.Trim();
            }

            if (string.IsNullOrEmpty(id))
                id = text;
            if (!graph.Nodes.TryGetValue(id, out var node))
            {
                node = new MermaidNode { Id = id, Text = text, Shape = shape, Subgraph = subgraph };
                graph.Nodes[id] = node;
            }
            return node;
        }
    }

    public static class MermaidLayout
    {
        public static void Apply(MermaidGraph graph)
        {
            var layers = AssignLayers(graph);
            // Zwiększony spacing dla dużych diagramów
            const int layerSpacing = 320;
            const int nodeSpacing = 200;
            const int minNodeGap = 40; // minimalny odstęp między węzłami

            foreach (var (layerIndex, nodes) in layers)
            {
                // Rozmieść węzły w warstwie równomiernie na osi poprzecznej
                int count = nodes.Count;
                double totalHeight = count * nodes[0].Size.Height + (count - 1) * minNodeGap;
                double startY = 40;
                if (graph.LeftToRight)
                {
                    startY = Math.Max(40, 40 + (1000 - totalHeight) / 2); // 1000 to przykładowa wysokość płótna
                }
                else
                {
                    startY = Math.Max(40, 40 + (1000 - totalHeight) / 2);
                }
                for (int i = 0; i < count; i++)
                {
                    var node = nodes[i];
                    if (graph.LeftToRight)
                        node.Position = new Point(40 + layerIndex * layerSpacing, startY + i * (node.Size.Height + minNodeGap));
                    else
                        node.Position = new Point(startY + i * (node.Size.Width + minNodeGap), 40 + layerIndex * layerSpacing);
                }
            }

            AssignPorts(graph);
            IndexPorts(graph);
        }

        private static Dictionary<int, List<MermaidNode>> AssignLayers(MermaidGraph graph)
        {
            var indegree = graph.Nodes.Values.ToDictionary(n => n, _ => 0);
            foreach (var e in graph.Edges) indegree[e.To]++;

            var layers = new Dictionary<int, List<MermaidNode>>();
            var q = new Queue<(MermaidNode n, int l)>();

            foreach (var (n, d) in indegree)
                if (d == 0) q.Enqueue((n, 0));

            while (q.Count > 0)
            {
                var (n, l) = q.Dequeue();
                if (!layers.TryGetValue(l, out var list)) layers[l] = list = new();
                if (!list.Contains(n)) list.Add(n);

                foreach (var e in graph.Edges.Where(e => e.From == n))
                {
                    indegree[e.To]--;
                    if (indegree[e.To] == 0)
                        q.Enqueue((e.To, l + 1));
                }
            }

            return layers;
        }

        private static void AssignPorts(MermaidGraph graph)
        {
            foreach (var e in graph.Edges)
            {
                if (graph.LeftToRight)
                {
                    e.FromPort = NodePort.Right;
                    e.ToPort = NodePort.Left;
                }
                else
                {
                    e.FromPort = NodePort.Bottom;
                    e.ToPort = NodePort.Top;
                }
            }
        }

        // Rozkład portów równomiernie na krawędzi węzła
        private static void IndexPorts(MermaidGraph graph)
        {
            var outgoing = new Dictionary<MermaidNode, List<MermaidEdge>>();
            var incoming = new Dictionary<MermaidNode, List<MermaidEdge>>();

            foreach (var node in graph.Nodes.Values)
            {
                outgoing[node] = new List<MermaidEdge>();
                incoming[node] = new List<MermaidEdge>();
            }

            foreach (var e in graph.Edges)
            {
                outgoing[e.From].Add(e);
                incoming[e.To].Add(e);
            }

            // Rozkład portów na krawędzi (dla LeftToRight: pionowo na lewej/prawej, dla TopDown: poziomo na górze/dole)
            foreach (var node in graph.Nodes.Values)
            {
                // OUT
                var outEdges = outgoing[node];
                int outCount = outEdges.Count;
                for (int i = 0; i < outCount; i++)
                {
                    var e = outEdges[i];
                    if (graph.LeftToRight)
                    {
                        double step = node.Size.Height / (outCount + 1);
                        double y = node.Position.Y + step * (i + 1);
                        e.FromPortOffset = new Vector(0, y - (node.Position.Y + node.Size.Height / 2));
                    }
                    else
                    {
                        double step = node.Size.Width / (outCount + 1);
                        double x = node.Position.X + step * (i + 1);
                        e.FromPortOffset = new Vector(x - (node.Position.X + node.Size.Width / 2), 0);
                    }
                }
                // IN
                var inEdges = incoming[node];
                int inCount = inEdges.Count;
                for (int i = 0; i < inCount; i++)
                {
                    var e = inEdges[i];
                    if (graph.LeftToRight)
                    {
                        double step = node.Size.Height / (inCount + 1);
                        double y = node.Position.Y + step * (i + 1);
                        e.ToPortOffset = new Vector(0, y - (node.Position.Y + node.Size.Height / 2));
                    }
                    else
                    {
                        double step = node.Size.Width / (inCount + 1);
                        double x = node.Position.X + step * (i + 1);
                        e.ToPortOffset = new Vector(x - (node.Position.X + node.Size.Width / 2), 0);
                    }
                }
            }
        }
    }

    public sealed class MermaidControl : Control
    {
        public MermaidGraph Graph { get; set; } = new();

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Graph == null || Graph.Nodes.Count == 0)
                return new Size(100, 100);

            double minX = Graph.Nodes.Values.Min(n => n.Position.X);
            double minY = Graph.Nodes.Values.Min(n => n.Position.Y);
            double maxX = Graph.Nodes.Values.Max(n => n.Position.X + n.Size.Width);
            double maxY = Graph.Nodes.Values.Max(n => n.Position.Y + n.Size.Height);

            double width = Math.Max(100, maxX - minX + 200); // większy margines z prawej
            double height = Math.Max(100, maxY - minY + 40);

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return base.ArrangeOverride(finalSize);
        }

        public override void Render(DrawingContext ctx)
        {
            foreach (var e in Graph.Edges)
                DrawEdge(ctx, e);

            DrawSubgraphs(ctx);

            foreach (var n in Graph.Nodes.Values)
                DrawNode(ctx, n);
        }

        private void DrawNode(DrawingContext ctx, MermaidNode n)
        {
            var pen = new Pen(new SolidColorBrush(n.BorderColor), n.BorderThickness);
            var brush = new SolidColorBrush(n.FillColor);
            var r = new Rect(n.Position, n.Size);

            switch (n.Shape)
            {
                case NodeShape.Rectangle:
                    ctx.DrawRectangle(brush, pen, r);
                    break;
                case NodeShape.RoundedRectangle:
                    ctx.DrawRectangle(brush, pen, r, 6);
                    break;
                case NodeShape.Diamond:
                    var points = new[]
                    {
                        new Point(r.Center.X, r.Top),
                        new Point(r.Right, r.Center.Y),
                        new Point(r.Center.X, r.Bottom),
                        new Point(r.Left, r.Center.Y)
                    };
                    var geometry = new StreamGeometry();
                    using (var gc = geometry.Open())
                    {
                        gc.BeginFigure(points[0], true);
                        for (int i = 1; i < points.Length; i++)
                        {
                            gc.LineTo(points[i]);
                        }
                        gc.LineTo(points[0]);
                    }
                    ctx.DrawGeometry(brush, pen, geometry);
                    break;
                case NodeShape.Circle:
                    ctx.DrawEllipse(brush, pen, r.Center, r.Width / 2, r.Height / 2);
                    break;
                case NodeShape.Stadium:
                    // Stadium: rectangle with semicircle ends
                    double radius = Math.Min(r.Height, r.Width) / 2;
                    var stadiumGeometry = new StreamGeometry();
                    using (var gc = stadiumGeometry.Open())
                    {
                        gc.BeginFigure(new Point(r.Left + radius, r.Top), true);
                        gc.LineTo(new Point(r.Right - radius, r.Top));
                        gc.ArcTo(new Point(r.Right - radius, r.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                        gc.LineTo(new Point(r.Left + radius, r.Bottom));
                        gc.ArcTo(new Point(r.Left + radius, r.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                    }
                    ctx.DrawGeometry(brush, pen, stadiumGeometry);
                    break;
                case NodeShape.DoubleRectangle:
                    ctx.DrawRectangle(brush, pen, r);
                    var inner = r.Deflate(6);
                    ctx.DrawRectangle(null, pen, inner);
                    break;
                case NodeShape.Hexagon:
                    var hexPoints = new Point[6];
                    for (int i = 0; i < 6; i++)
                    {
                        double angle = Math.PI / 3 * i;
                        hexPoints[i] = new Point(
                            r.Center.X + (r.Width / 2) * Math.Cos(angle),
                            r.Center.Y + (r.Height / 2) * Math.Sin(angle));
                    }
                    var hexGeometry = new StreamGeometry();
                    using (var gc = hexGeometry.Open())
                    {
                        gc.BeginFigure(hexPoints[0], true);
                        for (int i = 1; i < 6; i++)
                            gc.LineTo(hexPoints[i]);
                        gc.LineTo(hexPoints[0]);
                    }
                    ctx.DrawGeometry(brush, pen, hexGeometry);
                    break;
            }

            double fontSize = Math.Max(8, Math.Min(14, n.Size.Width / Math.Max(1, n.Text.Length)));
            var ft = new FormattedText(n.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize, Brushes.Black);
            ctx.DrawText(ft, r.Center - new Vector(ft.Width / 2, ft.Height / 2));
        }

        private void DrawEdge(DrawingContext ctx, MermaidEdge e)
        {
            double thickness = e.Style == EdgeStyle.Bold ? 4 : e.Style == EdgeStyle.Thick ? 3 : 2;
            var pen = new Pen(new SolidColorBrush(e.StrokeColor), thickness)
            {
                DashStyle = e.Style switch
                {
                    EdgeStyle.Solid => new DashStyle(),
                    EdgeStyle.Dashed => new DashStyle(new double[] { 6, 4 }, 0),
                    EdgeStyle.Dotted => new DashStyle(new double[] { 2, 2 }, 0),
                    _ => new DashStyle()
                }
            };

            var start = e.From.GetPort(e.FromPort) + e.FromPortOffset;
            var end = e.To.GetPort(e.ToPort) + e.ToPortOffset;
            var path = new List<Point> { start };

            if (Graph.LeftToRight)
            {
                var mx = (start.X + end.X) / 2;
                path.Add(new Point(mx, start.Y));
                path.Add(new Point(mx, end.Y));
            }
            else
            {
                var my = (start.Y + end.Y) / 2;
                path.Add(new Point(start.X, my));
                path.Add(new Point(end.X, my));
            }

            path.Add(end);
            for (int i = 0; i < path.Count - 1; i++) ctx.DrawLine(pen, path[i], path[i + 1]);
            DrawEdgeHead(ctx, path[^2], end, e.ArrowHead, pen.Brush);

            if (!string.IsNullOrEmpty(e.Label))
            {
                // Wyznacz środkowy odcinek linii
                int segIdx = Graph.LeftToRight ? 1 : 1;
                var segStart = path[segIdx];
                var segEnd = path[segIdx + 1];
                var isHorizontal = Math.Abs(segStart.Y - segEnd.Y) < 1;
                var isVertical = Math.Abs(segStart.X - segEnd.X) < 1;
                var ft = new FormattedText(e.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Black);
                var segVec = segEnd - segStart;
                var segLen = Math.Sqrt(segVec.X * segVec.X + segVec.Y * segVec.Y);
                Point labelPos;
                if (isHorizontal)
                {
                    // Jeśli etykieta dłuższa niż linia, wydłuż linię
                    double labelWidth = ft.Width;
                    double extra = Math.Max(0, labelWidth - segLen + 12);
                    if (extra > 0)
                    {
                        var newStart = new Point(segStart.X - extra / 2, segStart.Y);
                        var newEnd = new Point(segEnd.X + extra / 2, segEnd.Y);
                        ctx.DrawLine(pen, newStart, newEnd);
                        segStart = newStart;
                        segEnd = newEnd;
                    }
                    // Rysuj etykietę nad linią
                    labelPos = new Point((segStart.X + segEnd.X) / 2 - labelWidth / 2, segStart.Y - ft.Height - 2);
                }
                else if (isVertical)
                {
                    double labelHeight = ft.Height;
                    double extra = Math.Max(0, labelHeight - segLen + 12);
                    if (extra > 0)
                    {
                        var newStart = new Point(segStart.X, segStart.Y - extra / 2);
                        var newEnd = new Point(segEnd.X, segEnd.Y + extra / 2);
                        ctx.DrawLine(pen, newStart, newEnd);
                        segStart = newStart;
                        segEnd = newEnd;
                    }
                    // Rysuj etykietę z lewej strony linii
                    labelPos = new Point(segStart.X - ft.Width - 4, (segStart.Y + segEnd.Y) / 2 - ft.Height / 2);
                }
                else
                {
                    // Skośne – rysuj nad środkiem odcinka
                    labelPos = ((segStart + segEnd) / 2) - new Vector(ft.Width / 2, ft.Height + 2);
                }
                ctx.DrawText(ft, labelPos);
            }
        }

        private void DrawEdgeHead(DrawingContext ctx, Point from, Point tip, EdgeArrowHead head, IBrush brush)
        {
            var dir = new Vector(tip.X - from.X, tip.Y - from.Y);
            dir = Normalize(dir);
            switch (head)
            {
                case EdgeArrowHead.Arrow:
                    var left = tip - dir * 8 + new Vector(-dir.Y, dir.X) * 4;
                    var right = tip - dir * 8 + new Vector(dir.Y, -dir.X) * 4;
                    ctx.DrawLine(new Pen(brush, 1), left, tip);
                    ctx.DrawLine(new Pen(brush, 1), right, tip);
                    break;
                case EdgeArrowHead.Open:
                    var openLeft = tip - dir * 8 + new Vector(-dir.Y, dir.X) * 4;
                    var openRight = tip - dir * 8 + new Vector(dir.Y, -dir.X) * 4;
                    ctx.DrawLine(new Pen(brush, 1), openLeft, tip);
                    ctx.DrawLine(new Pen(brush, 1), openRight, tip);
                    break;
                case EdgeArrowHead.Circle:
                    ctx.DrawEllipse(null, new Pen(brush, 2), tip, 5, 5);
                    break;
                case EdgeArrowHead.Cross:
                    var cross1a = tip + new Vector(-6, -6);
                    var cross1b = tip + new Vector(6, 6);
                    var cross2a = tip + new Vector(-6, 6);
                    var cross2b = tip + new Vector(6, -6);
                    ctx.DrawLine(new Pen(brush, 2), cross1a, cross1b);
                    ctx.DrawLine(new Pen(brush, 2), cross2a, cross2b);
                    break;
                case EdgeArrowHead.None:
                default:
                    break;
            }
        }

        private static Vector Normalize(Vector v)
        {
            var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            return len > 0 ? new Vector(v.X / len, v.Y / len) : new Vector(0, 0);
        }

        private void DrawSubgraphs(DrawingContext ctx)
        {
            var subgroups = Graph.Nodes.Values.Where(n => n.Subgraph != null).GroupBy(n => n.Subgraph);

            foreach (var group in subgroups)
            {
                var minX = group.Min(n => n.Position.X);
                var minY = group.Min(n => n.Position.Y);
                var maxX = group.Max(n => n.Position.X + n.Size.Width);
                var maxY = group.Max(n => n.Position.Y + n.Size.Height);

                var rect = new Rect(minX - 20, minY - 20, (maxX - minX) + 40, (maxY - minY) + 40);
                ctx.DrawRectangle(null, new Pen(Brushes.Gray, 2, dashStyle: new DashStyle(new double[] { 4, 4 }, 0)), rect);

                var ft = new FormattedText(group.Key!, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.Gray);
                ctx.DrawText(ft, new Point(rect.Left + 10, rect.Top + 5));
            }
        }
    }

    public static class MermaidFactory
    {
        public static MermaidControl Create(string source)
        {
            var g = MermaidParser.Parse(source);
            MermaidLayout.Apply(g);
            return new MermaidControl
            {
                Graph = g,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 0, 12)
            };
        }
    }
}