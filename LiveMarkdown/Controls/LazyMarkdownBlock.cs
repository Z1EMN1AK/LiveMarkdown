using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace LiveMarkdown.Controls
{
    /// <summary>
    /// Represents a markdown block that can be in one of two states:
    /// - Placeholder: lightweight Border with estimated height (minimal memory)
    /// - Materialized: full MarkdownView control with rendered content
    /// </summary>
    public class LazyMarkdownBlock : IDisposable
    {
        private string _rawText;
        private double _estimatedHeight;
        private Border _placeholder;
        private MarkdownView? _materializedControl;
        private bool _isDisposed;

        /// <summary>
        /// The raw markdown text for this block.
        /// </summary>
        public string RawText => _rawText;

        /// <summary>
        /// Estimated height used for scroll calculations when not materialized.
        /// Updated with actual height when dematerialized.
        /// </summary>
        public double EstimatedHeight => _estimatedHeight;

        /// <summary>
        /// Whether this block is currently materialized (full control) or just a placeholder.
        /// </summary>
        public bool IsMaterialized => _materializedControl != null;

        /// <summary>
        /// The placeholder control shown when not materialized.
        /// </summary>
        public Border Placeholder => _placeholder;

        /// <summary>
        /// The materialized MarkdownView control (null if not materialized).
        /// </summary>
        public MarkdownView? MaterializedControl => _materializedControl;

        public LazyMarkdownBlock(string rawText, double estimatedHeight = 50.0)
        {
            _rawText = rawText;
            _estimatedHeight = Math.Max(20, estimatedHeight);
            
            // Create lightweight placeholder
            _placeholder = CreatePlaceholder();
        }

        private Border CreatePlaceholder()
        {
            // Extremely lightweight placeholder - just a sized container
            var placeholder = new Border
            {
                Height = _estimatedHeight,
                Background = Brushes.Transparent,
                // Optional: show loading indicator for very large blocks
                // Child = new TextBlock { Text = "...", Opacity = 0.3 }
            };

            placeholder.Classes.Add("md-lazy-placeholder");

            return placeholder;
        }

        /// <summary>
        /// Update the raw text for this block (used during streaming).
        /// </summary>
        public void UpdateText(string newText)
        {
            _rawText = newText;

            // If materialized, update the control
            if (_materializedControl != null)
            {
                _materializedControl.Text = newText;
            }

            // Update estimated height based on text length
            UpdateEstimatedHeightFromText();
        }

        private void UpdateEstimatedHeightFromText()
        {
            // Rough estimate: ~20px per line, ~80 chars per line
            var lineCount = Math.Max(1, _rawText.Split('\n').Length);
            var charBasedLines = _rawText.Length / 80.0;
            var estimatedLines = Math.Max(lineCount, charBasedLines);
            
            // Add extra for code blocks, headers, etc.
            if (_rawText.Contains("```"))
                estimatedLines += 4;
            if (_rawText.StartsWith("#"))
                estimatedLines += 1;

            var newHeight = Math.Max(30, estimatedLines * 22);
            
            if (Math.Abs(newHeight - _estimatedHeight) > 10)
            {
                _estimatedHeight = newHeight;
                _placeholder.Height = _estimatedHeight;
            }
        }

        /// <summary>
        /// Update estimated height (usually called with actual measured height).
        /// </summary>
        public void UpdateEstimatedHeight(double height)
        {
            if (height > 0)
            {
                _estimatedHeight = height;
                _placeholder.Height = height;
            }
        }

        /// <summary>
        /// Materialize this block by creating the full MarkdownView control.
        /// </summary>
        public void Materialize(MarkdownView control)
        {
            if (_isDisposed)
                return;

            _materializedControl = control;
            _materializedControl.Text = _rawText;
        }

        /// <summary>
        /// Dematerialize this block - dispose the control and keep only placeholder.
        /// </summary>
        public void Dematerialize()
        {
            if (_materializedControl == null)
                return;

            // Store actual height before disposing
            var actualHeight = _materializedControl.Bounds.Height;
            if (actualHeight > 0)
            {
                _estimatedHeight = actualHeight;
                _placeholder.Height = actualHeight;
            }

            // Dispose the MarkdownView if it's disposable
            if (_materializedControl is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _materializedControl = null;
        }

        /// <summary>
        /// Dispose all resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            Dematerialize();

            _placeholder = null!;
            _rawText = null!;

            GC.SuppressFinalize(this);
        }
    }
}
