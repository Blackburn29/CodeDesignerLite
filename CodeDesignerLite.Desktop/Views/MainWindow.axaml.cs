using Avalonia.Controls;
using CodeDesignerLite.Desktop.ViewModels;
using CodeDesignerLite.Desktop.Services;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using ReactiveUI;
using System;
using System.Reflection;
using System.Xml;

namespace CodeDesignerLite.Desktop.Views;

public partial class MainWindow : Window
{
    private ErrorLineTransformer? _errorLineTransformer;

    public MainWindow()
    {
        InitializeComponent();

        this.Opened += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var editor = this.FindControl<TextEditor>("InputEditor");
                if (editor != null)
                {
                    editor.Document = vm.InputDocument;

                    LoadSyntaxHighlighting(editor);

                    _errorLineTransformer = new ErrorLineTransformer();
                    editor.TextArea.TextView.LineTransformers.Add(_errorLineTransformer);

                    vm.WhenAnyValue(x => x.ErrorLineNumbers)
                        .Subscribe(errorLines =>
                        {
                            if (_errorLineTransformer != null)
                            {
                                _errorLineTransformer.SetErrorLines(errorLines);
                                editor.TextArea.TextView.Redraw();
                            }
                        });
                }
            }
        };
    }

    private void LoadSyntaxHighlighting(TextEditor editor)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "CodeDesignerLite.Desktop.Highlighting.MipsAssembly.xshd";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                editor.SyntaxHighlighting = highlighting;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load syntax highlighting: {ex.Message}");
        }
    }
}
