using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace MermaidEditor;

/// <summary>
/// Draws squiggly red underlines beneath misspelled words in the AvalonEdit text editor.
/// Uses a debounced background check to avoid blocking the UI thread.
/// </summary>
public class SpellCheckBackgroundRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly SpellCheckService _spellCheckService;
    private readonly DispatcherTimer _recheckTimer;
    private List<MisspelledWord> _misspelledWords = new();
    private CancellationTokenSource? _checkCts;
    private bool _isMermaid;

    // Squiggly line visual settings
    private static readonly System.Windows.Media.Pen SquigglyPen;

    static SpellCheckBackgroundRenderer()
    {
        // Create a red squiggly pen using a dash pattern that approximates a squiggly line
        SquigglyPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x40, 0x40)), 1.2)
        {
            DashStyle = new DashStyle(new[] { 1.0, 2.0 }, 0)
        };
        SquigglyPen.Freeze();
    }

    public SpellCheckBackgroundRenderer(TextEditor editor, SpellCheckService spellCheckService)
    {
        _editor = editor;
        _spellCheckService = spellCheckService;

        // Debounce timer: recheck spelling 500ms after last text change
        _recheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _recheckTimer.Tick += (_, _) =>
        {
            _recheckTimer.Stop();
            _ = RecheckSpellingAsync();
        };
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public bool IsMermaid
    {
        get => _isMermaid;
        set
        {
            if (_isMermaid != value)
            {
                _isMermaid = value;
                InvalidateSpelling();
            }
        }
    }

    /// <summary>
    /// Gets the list of currently detected misspelled words.
    /// </summary>
    public IReadOnlyList<MisspelledWord> MisspelledWords => _misspelledWords;

    /// <summary>
    /// Schedules a spelling recheck (debounced).
    /// Call this when the document text changes.
    /// </summary>
    public void InvalidateSpelling()
    {
        _recheckTimer.Stop();
        _recheckTimer.Start();
    }

    /// <summary>
    /// Gets the misspelled word at the given document offset, if any.
    /// </summary>
    public MisspelledWord? GetMisspelledWordAtOffset(int offset)
    {
        return _misspelledWords.FirstOrDefault(w =>
            offset >= w.StartOffset && offset < w.StartOffset + w.Length);
    }

    /// <summary>
    /// Performs a full spelling check on the current document.
    /// </summary>
    private async Task RecheckSpellingAsync()
    {
        if (!_spellCheckService.IsLoaded) return;

        // Cancel any previous check
        _checkCts?.Cancel();
        _checkCts = new CancellationTokenSource();
        var token = _checkCts.Token;

        try
        {
            var document = _editor.Document;
            var fullText = document.Text;
            var isMermaid = _isMermaid;

            // Run the spell check on a background thread
            var results = await Task.Run(() =>
            {
                var misspelled = new List<MisspelledWord>();
                if (string.IsNullOrEmpty(fullText)) return misspelled;

                // Determine which lines are inside code blocks
                var codeBlockLines = SpellCheckService.GetCodeBlockLines(fullText);

                var lines = fullText.Split('\n');
                int currentOffset = 0;

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    token.ThrowIfCancellationRequested();

                    var line = lines[lineIndex];

                    // Skip lines inside code blocks
                    if (!codeBlockLines.Contains(lineIndex))
                    {
                        var lineResults = _spellCheckService.CheckLine(line, currentOffset, isMermaid);
                        misspelled.AddRange(lineResults);
                    }

                    currentOffset += line.Length + 1; // +1 for the \n
                }

                return misspelled;
            }, token);

            if (!token.IsCancellationRequested)
            {
                _misspelledWords = results;
                _editor.TextArea.TextView.InvalidateLayer(Layer);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when a new check is triggered before the old one completes
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpellCheck render error: {ex.Message}");
        }
    }

    /// <summary>
    /// Draws the squiggly underlines for misspelled words.
    /// </summary>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_misspelledWords.Count == 0) return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        int viewStart = visualLines.First().FirstDocumentLine.Offset;
        int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

        foreach (var word in _misspelledWords)
        {
            // Only draw words that are visible
            if (word.StartOffset + word.Length < viewStart || word.StartOffset > viewEnd)
                continue;

            // Clamp to document bounds
            int docLength = textView.Document.TextLength;
            if (word.StartOffset >= docLength) continue;
            int endOffset = Math.Min(word.StartOffset + word.Length, docLength);

            var segment = new TextSegment
            {
                StartOffset = word.StartOffset,
                Length = endOffset - word.StartOffset
            };

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                // Draw squiggly line at the bottom of the text
                double y = rect.Bottom - 1;
                double startX = rect.Left;
                double endX = rect.Right;

                // Create a squiggly path
                var geometry = CreateSquigglyGeometry(startX, endX, y);
                if (geometry != null)
                {
                    drawingContext.DrawGeometry(null, SquigglyPen, geometry);
                }
            }
        }
    }

    /// <summary>
    /// Creates a squiggly line geometry (wave pattern) between two X coordinates at a given Y.
    /// </summary>
    private static StreamGeometry? CreateSquigglyGeometry(double startX, double endX, double y)
    {
        if (endX - startX < 2) return null;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(startX, y), false, false);

            double x = startX;
            double waveHeight = 2.0;
            double waveLength = 4.0;
            bool up = true;

            while (x < endX)
            {
                double nextX = Math.Min(x + waveLength / 2, endX);
                double nextY = up ? y - waveHeight : y;

                ctx.LineTo(new System.Windows.Point(nextX, nextY), true, false);
                x = nextX;
                up = !up;
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Clears all misspelled words and redraws.
    /// </summary>
    public void Clear()
    {
        _recheckTimer.Stop();
        _checkCts?.Cancel();
        _misspelledWords.Clear();
        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }
}
