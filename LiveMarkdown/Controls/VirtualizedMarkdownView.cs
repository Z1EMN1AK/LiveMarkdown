using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace LiveMarkdown.Controls
{
    /// <summary>
    /// A virtualized markdown view that only materializes blocks within the visible viewport + buffer.
    /// Blocks outside the visible area are replaced with lightweight placeholders to reduce memory usage.
    /// </summary>
    public class VirtualizedMarkdownView : TemplatedControl, IDisposable
    {
        #region Styled Properties

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<VirtualizedMarkdownView, string?>(nameof(Text));

        public static readonly StyledProperty<int> BufferBlocksProperty =
            AvaloniaProperty.Register<VirtualizedMarkdownView, int>(nameof(BufferBlocks), defaultValue: 5);

        public static readonly StyledProperty<double> EstimatedBlockHeightProperty =
            AvaloniaProperty.Register<VirtualizedMarkdownView, double>(nameof(EstimatedBlockHeight), defaultValue: 50.0);

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// Number of blocks to keep materialized above and below the visible viewport.
        /// </summary>
        public int BufferBlocks
        {
            get => GetValue(BufferBlocksProperty);
            set => SetValue(BufferBlocksProperty, value);
        }

        /// <summary>
        /// Estimated height for unmaterialized blocks (used for scroll calculations).
        /// </summary>
        public double EstimatedBlockHeight
        {
            get => GetValue(EstimatedBlockHeightProperty);
            set => SetValue(EstimatedBlockHeightProperty, value);
        }

        #endregion

        #region Private Fields

        private ScrollViewer? _scrollViewer;
        private Panel? _contentPanel;
        private readonly List<LazyMarkdownBlock> _blocks = new();
        private bool _isDisposed;
        private double _lastScrollOffset;
        private Size _lastViewportSize;

        #endregion

        #region Lifecycle

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            _contentPanel = e.NameScope.Find<Panel>("PART_ContentPanel");

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }

            UpdateBlocks();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                UpdateBlocks();
            }
        }

        #endregion

        #region Block Management

        private void UpdateBlocks()
        {
            if (_contentPanel == null)
                return;

            var text = Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                ClearAllBlocks();
                return;
            }

            // Split text into logical blocks (paragraphs separated by double newlines)
            var blockTexts = SplitIntoBlocks(text);

            // Check if this is an append operation
            bool isAppend = IsAppendOperation(blockTexts);

            if (isAppend)
            {
                // Only add new blocks
                for (int i = _blocks.Count; i < blockTexts.Count; i++)
                {
                    var block = new LazyMarkdownBlock(blockTexts[i], EstimatedBlockHeight);
                    _blocks.Add(block);
                    _contentPanel.Children.Add(block.Placeholder);
                }

                // Update the last block (it might have been extended)
                if (_blocks.Count > 0 && blockTexts.Count > 0)
                {
                    int lastIndex = _blocks.Count - 1;
                    if (lastIndex < blockTexts.Count)
                    {
                        _blocks[lastIndex].UpdateText(blockTexts[lastIndex]);
                    }
                }
            }
            else
            {
                // Full rebuild
                ClearAllBlocks();

                foreach (var blockText in blockTexts)
                {
                    var block = new LazyMarkdownBlock(blockText, EstimatedBlockHeight);
                    _blocks.Add(block);
                    _contentPanel.Children.Add(block.Placeholder);
                }
            }

            // Materialize visible blocks
            Dispatcher.UIThread.Post(UpdateMaterialization, DispatcherPriority.Background);
        }

        private bool IsAppendOperation(List<string> newBlocks)
        {
            if (_blocks.Count == 0 || newBlocks.Count < _blocks.Count)
                return false;

            // Check if existing blocks match
            for (int i = 0; i < _blocks.Count - 1; i++) // -1 to allow last block to change
            {
                if (i >= newBlocks.Count || _blocks[i].RawText != newBlocks[i])
                    return false;
            }

            return true;
        }

        private List<string> SplitIntoBlocks(string text)
        {
            var blocks = new List<string>();
            
            // Split by double newlines or code blocks
            var lines = text.Split('\n');
            var currentBlock = new System.Text.StringBuilder();
            bool inCodeBlock = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.TrimStart();
                
                // Check for fenced code block markers
                if (trimmedLine.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End of code block
                        currentBlock.AppendLine(line);
                        blocks.Add(currentBlock.ToString().TrimEnd());
                        currentBlock.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        // Start of code block - save previous content first
                        if (currentBlock.Length > 0)
                        {
                            blocks.Add(currentBlock.ToString().TrimEnd());
                            currentBlock.Clear();
                        }
                        currentBlock.AppendLine(line);
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    currentBlock.AppendLine(line);
                    continue;
                }

                // Empty line indicates block separator (outside code blocks)
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentBlock.Length > 0)
                    {
                        blocks.Add(currentBlock.ToString().TrimEnd());
                        currentBlock.Clear();
                    }
                }
                else
                {
                    currentBlock.AppendLine(line);
                }
            }

            // Don't forget the last block
            if (currentBlock.Length > 0)
            {
                blocks.Add(currentBlock.ToString().TrimEnd());
            }

            return blocks;
        }

        private void ClearAllBlocks()
        {
            foreach (var block in _blocks)
            {
                block.Dispose();
            }
            _blocks.Clear();
            _contentPanel?.Children.Clear();
        }

        #endregion

        #region Virtualization

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null)
                return;

            var offset = _scrollViewer.Offset.Y;
            var viewportSize = _scrollViewer.Viewport;

            // Only update if scroll changed significantly
            if (Math.Abs(offset - _lastScrollOffset) > 10 || 
                Math.Abs(viewportSize.Height - _lastViewportSize.Height) > 10)
            {
                _lastScrollOffset = offset;
                _lastViewportSize = viewportSize;
                UpdateMaterialization();
            }
        }

        private void UpdateMaterialization()
        {
            if (_scrollViewer == null || _contentPanel == null || _blocks.Count == 0)
                return;

            var viewportTop = _scrollViewer.Offset.Y;
            var viewportBottom = viewportTop + _scrollViewer.Viewport.Height;

            // Calculate which blocks are visible (with buffer)
            double currentY = 0;
            int firstVisible = -1;
            int lastVisible = -1;

            for (int i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                var blockHeight = block.IsMaterialized 
                    ? block.MaterializedControl?.Bounds.Height ?? EstimatedBlockHeight 
                    : EstimatedBlockHeight;

                var blockTop = currentY;
                var blockBottom = currentY + blockHeight;

                // Check if block is in visible range (with buffer)
                bool isInRange = blockBottom >= viewportTop - (BufferBlocks * EstimatedBlockHeight) &&
                                 blockTop <= viewportBottom + (BufferBlocks * EstimatedBlockHeight);

                if (isInRange)
                {
                    if (firstVisible < 0) firstVisible = i;
                    lastVisible = i;
                }

                currentY = blockBottom;
            }

            // Materialize blocks in range, dematerialize others
            for (int i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                bool shouldBeMaterialized = i >= firstVisible && i <= lastVisible;

                if (shouldBeMaterialized && !block.IsMaterialized)
                {
                    MaterializeBlock(block, i);
                }
                else if (!shouldBeMaterialized && block.IsMaterialized)
                {
                    DematerializeBlock(block, i);
                }
            }
        }

        private void MaterializeBlock(LazyMarkdownBlock block, int index)
        {
            if (_contentPanel == null || block.IsMaterialized)
                return;

            // Create the actual MarkdownView for this block
            var markdownView = new MarkdownView
            {
                Text = block.RawText
            };

            block.Materialize(markdownView);

            // Replace placeholder with materialized control
            if (index < _contentPanel.Children.Count)
            {
                _contentPanel.Children[index] = markdownView;
            }
        }

        private void DematerializeBlock(LazyMarkdownBlock block, int index)
        {
            if (_contentPanel == null || !block.IsMaterialized)
                return;

            // Store actual height before dematerializing
            if (block.MaterializedControl != null)
            {
                block.UpdateEstimatedHeight(block.MaterializedControl.Bounds.Height);
            }

            block.Dematerialize();

            // Replace with placeholder
            if (index < _contentPanel.Children.Count)
            {
                _contentPanel.Children[index] = block.Placeholder;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            }

            ClearAllBlocks();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
