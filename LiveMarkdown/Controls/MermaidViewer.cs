using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace LiveMarkdown.Controls.Mermaid
{
    public class MermaidViewer : UserControl
    {
        private readonly Canvas _canvas;
        private readonly Border _border;
        private readonly MermaidControl _diagram;
        private double _zoom = 1.0;
        private Point _offset = new(0, 0);
        private Point? _lastDrag;
        private Window? _popupWindow;
        private bool _fitDone = false;
        private bool _fitRequested = false;

        public MermaidViewer(MermaidControl diagram)
        {
            _diagram = diagram;
            _canvas = new Canvas { Width = 2000, Height = 2000 };
            _canvas.Children.Add(_diagram);
            _border = new Border { Child = _canvas, Background = Brushes.White };

            var zoomInBtn = new Button { Content = "+", Width = 32, Height = 32, Margin = new Thickness(4) };
            var zoomOutBtn = new Button { Content = "?", Width = 32, Height = 32, Margin = new Thickness(4,40,4,4) };
            var resetBtn = new Button { Content = "?", Width = 32, Height = 32, Margin = new Thickness(4,76,4,4) };
            var popupBtn = new Button { Content = "?", Width = 32, Height = 32, Margin = new Thickness(4,112,4,4) };

            zoomInBtn.Click += (_, _) => SetZoom(_zoom * 1.05);
            zoomOutBtn.Click += (_, _) => SetZoom(_zoom / 1.05);
            resetBtn.Click += (_, _) => FitToView();
            popupBtn.Click += (_, _) => OpenInNewWindow();

            var btnPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 8, 0) };
            btnPanel.Children.Add(zoomInBtn);
            btnPanel.Children.Add(zoomOutBtn);
            btnPanel.Children.Add(resetBtn);
            btnPanel.Children.Add(popupBtn);

            var grid = new Grid();
            grid.Children.Add(_border);
            grid.Children.Add(btnPanel);
            Content = grid;

            _border.PointerWheelChanged += OnPointerWheelChanged;
            _border.PointerPressed += OnPointerPressed;
            _border.PointerReleased += OnPointerReleased;
            _border.PointerMoved += OnPointerMoved;
            _border.LayoutUpdated += (_, _) =>
            {
                if (!_fitDone && _border.Bounds.Width > 0 && _diagram.Bounds.Width > 0)
                {
                    FitToView();
                    _fitDone = true;
                }
                if (_fitRequested && _border.Bounds.Width > 0 && _diagram.Bounds.Width > 0)
                {
                    FitToView();
                    _fitRequested = false;
                }
            };
        }

        // Publiczne wywo³anie wyœrodkowania na ¿¹danie
        public void RequestFitToView()
        {
            _fitRequested = true;
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Clamp(zoom, 0.2, 5.0);
            SetTransforms();
        }

        private void SetOffset(Point offset)
        {
            // Usuniêto ograniczanie offsetu, aby umo¿liwiæ swobodne przesuwanie widoku
            _offset = offset;
            SetTransforms();
        }

        private void SetTransforms()
        {
            _canvas.RenderTransform = new TransformGroup
            {
                Children = new Transforms
                {
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_offset.X, _offset.Y)
                }
            };
        }

        // Upublicznij FitToView, aby mo¿na by³o wywo³aæ j¹ z zewn¹trz
        public void FitToView()
        {
            // Spróbuj dopasowaæ diagram do widoku
            var bounds = _diagram.Bounds;
            var viewSize = _border.Bounds.Size;
            if (bounds.Width == 0 || bounds.Height == 0 || viewSize.Width == 0 || viewSize.Height == 0)
                return;
            double scaleX = viewSize.Width / bounds.Width;
            double scaleY = viewSize.Height / bounds.Height;
            double scale = Math.Min(scaleX, scaleY) * 0.9; // margines
            double offsetX = (viewSize.Width - bounds.Width * scale) / 2 / scale - bounds.X;
            double offsetY = (viewSize.Height - bounds.Height * scale) / 2 / scale - bounds.Y;
            _zoom = scale;
            _offset = new Point(offsetX, offsetY);
            SetTransforms();
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var oldZoom = _zoom;
            SetZoom(_zoom + (e.Delta.Y > 0 ? 0.05 : -0.05));
            // Zoom to mouse position
            var pos = e.GetPosition(_border);
            _offset = new Point(
                (_offset.X - pos.X) * (_zoom / oldZoom) + pos.X,
                (_offset.Y - pos.Y) * (_zoom / oldZoom) + pos.Y
            );
            SetOffset(_offset); // u¿yj ograniczenia
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(_border).Properties.IsLeftButtonPressed)
                _lastDrag = e.GetPosition(_border);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _lastDrag = null;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_lastDrag.HasValue && e.GetCurrentPoint(_border).Properties.IsLeftButtonPressed)
            {
                var pos = e.GetPosition(_border);
                var delta = pos - _lastDrag.Value;
                _offset += delta;
                _lastDrag = pos;
                SetOffset(_offset); // u¿yj ograniczenia
            }
        }

        private void OpenInNewWindow()
        {
            if (_popupWindow != null)
            {
                _popupWindow.Activate();
                return;
            }
            var win = new Window
            {
                Title = "Podgl¹d diagramu Mermaid",
                Width = 1200,
                Height = 900,
                Background = Brushes.White,
                Content = new MermaidViewer(new MermaidControl { Graph = _diagram.Graph })
            };
            win.Opened += (_, _) =>
            {
                if (win.Content is MermaidViewer viewer)
                    viewer.FitToView(); // wyœrodkuj po otwarciu
            };
            win.Closed += (_, _) => _popupWindow = null;
            _popupWindow = win;
            win.Show();
        }
    }
}
