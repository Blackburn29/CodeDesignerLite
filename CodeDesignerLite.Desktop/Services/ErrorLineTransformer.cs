using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace CodeDesignerLite.Desktop.Services;

public class ErrorLineTransformer : DocumentColorizingTransformer
{
    private HashSet<int> _errorLines = new();
    private readonly IBrush _errorBackgroundBrush;

    public ErrorLineTransformer()
    {
        _errorBackgroundBrush = new SolidColorBrush(Color.FromRgb(240, 71, 71), 0.2);
    }

    public void SetErrorLines(IEnumerable<int> lineNumbers)
    {
        _errorLines = lineNumbers.ToHashSet();
    }

    public void ClearErrors()
    {
        _errorLines.Clear();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_errorLines.Contains(line.LineNumber))
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.BackgroundBrush = _errorBackgroundBrush;
            });
        }
    }
}
