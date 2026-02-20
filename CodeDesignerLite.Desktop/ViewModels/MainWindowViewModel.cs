using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CodeDesignerLite.Core.Compiler;
using CodeDesignerLite.Core.Enums;
using CodeDesignerLite.Core.Services;
using CodeDesignerLite.Desktop.Services;
using ReactiveUI;

namespace CodeDesignerLite.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const double CDL_Version = 1.05;

    private readonly MipsCompiler _compiler;
    private readonly FileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboardService;
    private readonly RecentFilesService _recentFilesService;

    private string? _currentFilePath;
    private bool _hasUnsavedChanges;

    private TextDocument _inputDocument;
    public TextDocument InputDocument
    {
        get => _inputDocument;
        set => this.RaiseAndSetIfChanged(ref _inputDocument, value);
    }

    private string _outputText = string.Empty;
    public string OutputText
    {
        get => _outputText;
        set => this.RaiseAndSetIfChanged(ref _outputText, value);
    }

    private int _fontSize = 10;
    public int FontSize
    {
        get => _fontSize;
        set => this.RaiseAndSetIfChanged(ref _fontSize, value);
    }

    private string _addressFormat = "-";
    public string AddressFormat
    {
        get => _addressFormat;
        set => this.RaiseAndSetIfChanged(ref _addressFormat, value);
    }

    private bool _isPS2Mode = true;
    public bool IsPS2Mode
    {
        get => _isPS2Mode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isPS2Mode, value);
            if (value) IsPnachMode = false;
            this.RaisePropertyChanged(nameof(SelectedOutputFormatIndex));
        }
    }

    private bool _isPnachMode = false;
    public bool IsPnachMode
    {
        get => _isPnachMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isPnachMode, value);
            if (value) IsPS2Mode = false;
            this.RaisePropertyChanged(nameof(SelectedOutputFormatIndex));
        }
    }

    public int SelectedOutputFormatIndex
    {
        get => IsPS2Mode ? 0 : 1;
        set
        {
            if (value == 0)
                IsPS2Mode = true;
            else if (value == 1)
                IsPnachMode = true;
        }
    }

    private string _windowTitle = "CodeDesignerLite - Untitled";
    public string WindowTitle
    {
        get => _windowTitle;
        set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
    }

    private int _outputLineCount = 0;
    public int OutputLineCount
    {
        get => _outputLineCount;
        set => this.RaiseAndSetIfChanged(ref _outputLineCount, value);
    }

    private List<int> _errorLineNumbers = [];
    public List<int> ErrorLineNumbers
    {
        get => _errorLineNumbers;
        set => this.RaiseAndSetIfChanged(ref _errorLineNumbers, value);
    }

    private List<string> _recentFiles = [];
    public List<string> RecentFiles
    {
        get => _recentFiles;
        set => this.RaiseAndSetIfChanged(ref _recentFiles, value);
    }

    private List<ErrorViewModel> _compilationErrors = [];
    public List<ErrorViewModel> CompilationErrors
    {
        get => _compilationErrors;
        set => this.RaiseAndSetIfChanged(ref _compilationErrors, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool HasErrors => _compilationErrors.Any();

    public string ErrorSummary => $"Errors ({_compilationErrors.Count})";

    public string OutputBorderBrush => HasErrors ? "#F04747" : "Transparent";

    public bool HasOpenFile => !string.IsNullOrEmpty(_currentFilePath);

    public bool HasInputText => !string.IsNullOrWhiteSpace(InputDocument?.Text);

    public ReactiveCommand<Unit, Unit> CompileCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyOutputCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<string, Unit> OpenRecentFileCommand { get; }

    public MainWindowViewModel()
        : this(new MipsCompiler(), new FileService(), new DialogService(), new ClipboardService(), new RecentFilesService())
    {
    }

    public MainWindowViewModel(
        MipsCompiler compiler,
        FileService fileService,
        IDialogService dialogService,
        IClipboardService clipboardService,
        RecentFilesService recentFilesService)
    {
        _compiler = compiler;
        _fileService = fileService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _recentFilesService = recentFilesService;

        _inputDocument = new TextDocument();
        _inputDocument.TextChanged += (s, e) =>
        {
            _hasUnsavedChanges = true;
            UpdateWindowTitle();
            this.RaisePropertyChanged(nameof(HasInputText));
        };

        var canSave = this.WhenAnyValue(x => x.HasOpenFile);
        var canCompile = this.WhenAnyValue(x => x.HasInputText);

        CompileCommand = ReactiveCommand.CreateFromTask(CompileAsync, canCompile);
        NewFileCommand = ReactiveCommand.CreateFromTask(NewFileAsync);
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
        SaveFileCommand = ReactiveCommand.CreateFromTask(SaveFileAsync, canSave);
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsFileAsync);
        CopyOutputCommand = ReactiveCommand.CreateFromTask(CopyOutputAsync);
        AboutCommand = ReactiveCommand.CreateFromTask(ShowAboutAsync);
        ExitCommand = ReactiveCommand.Create(Exit);
        OpenRecentFileCommand = ReactiveCommand.CreateFromTask<string>(OpenRecentFileAsync);

        RefreshRecentFiles();
    }

    private async Task CompileAsync()
    {
        try
        {
            var inputText = InputDocument.Text;
            var inputLines = inputText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var outputMode = IsPS2Mode ? OutputFormatMode.PS2 : OutputFormatMode.Pnach;
            var addressFormatChar = string.IsNullOrEmpty(AddressFormat) ? "-" : AddressFormat;

            var result = await _compiler.CompileAsync(inputLines, _currentFilePath ?? string.Empty, outputMode, addressFormatChar);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (result.Success)
                {
                    OutputText = result.Output ?? string.Empty;
                    OutputLineCount = !string.IsNullOrEmpty(OutputText) ? OutputText.Split('\n').Length : 0;
                    CompilationErrors = [];
                    ErrorLineNumbers = [];
                    StatusMessage = "Compilation succeeded";
                }
                else
                {
                    var errorCount = result.ErrorCount;
                    var errorMessage = $"\n*** Compilation failed ***\n*** {errorCount} error(s) ***\n*** See errors below ***";

                    var compiledOutput = result.Output ?? string.Empty;
                    OutputText = string.IsNullOrEmpty(compiledOutput)
                        ? errorMessage.TrimStart()
                        : $"{compiledOutput}{errorMessage}";

                    OutputLineCount = !string.IsNullOrEmpty(OutputText) ? OutputText.Split('\n').Length : 0;

                    CompilationErrors = result.Errors?
                        .Select(e => new ErrorViewModel
                        {
                            LineNumber = e.OriginalLineNumber,
                            FileName = e.FileName,
                            Message = e.ErrorMessage
                        })
                        .ToList() ?? [];

                    ErrorLineNumbers = result.ErrorLineNumbers ?? [];
                    StatusMessage = $"Compilation failed with {errorCount} error(s)";
                }

                this.RaisePropertyChanged(nameof(HasErrors));
                this.RaisePropertyChanged(nameof(ErrorSummary));
                this.RaisePropertyChanged(nameof(OutputBorderBrush));
            });
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Compilation failed: {ex.Message}");
        }
    }

    private async Task NewFileAsync()
    {
        if (!await CheckUnsavedChangesAsync())
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            InputDocument.Text = string.Empty;
            OutputText = string.Empty;
            OutputLineCount = 0;
            ErrorLineNumbers = [];
            CompilationErrors = [];
            _hasUnsavedChanges = false;
            SetCurrentFilePath(null);
            this.RaisePropertyChanged(nameof(HasErrors));
            this.RaisePropertyChanged(nameof(ErrorSummary));
        });
    }

    private async Task OpenFileAsync()
    {
        if (!await CheckUnsavedChangesAsync())
            return;

        try
        {
            var filePath = await _dialogService.ShowOpenFileDialogAsync();
            if (string.IsNullOrEmpty(filePath))
                return;

            var content = await _fileService.ReadFileAsync(filePath);
            var fileName = _fileService.GetFileName(filePath);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                InputDocument.Text = content;
                ErrorLineNumbers = [];
                CompilationErrors = [];
                _hasUnsavedChanges = false;
                SetCurrentFilePath(filePath);
                this.RaisePropertyChanged(nameof(HasErrors));
                this.RaisePropertyChanged(nameof(ErrorSummary));
                this.RaisePropertyChanged(nameof(OutputBorderBrush));
            });

            _recentFilesService.AddRecentFile(filePath);
            RefreshRecentFiles();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Failed to open file: {ex.Message}");
        }
    }

    private async Task SaveFileAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveAsFileAsync();
            return;
        }

        try
        {
            await _fileService.WriteFileAsync(_currentFilePath, InputDocument.Text);
            _hasUnsavedChanges = false;
            UpdateWindowTitle();
            StatusMessage = "File saved successfully";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Failed to save file: {ex.Message}");
        }
    }

    private async Task SaveAsFileAsync()
    {
        try
        {
            var filePath = await _dialogService.ShowSaveFileDialogAsync();
            if (string.IsNullOrEmpty(filePath))
                return;

            await _fileService.WriteFileAsync(filePath, InputDocument.Text);
            var fileName = _fileService.GetFileName(filePath);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _hasUnsavedChanges = false;
                SetCurrentFilePath(filePath);
            });

            StatusMessage = "File saved successfully";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Failed to save file: {ex.Message}");
        }
    }

    private async Task CopyOutputAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(OutputText))
            {
                StatusMessage = "No output to copy";
                return;
            }

            await _clipboardService.SetTextAsync(OutputText);
            StatusMessage = "Output copied to clipboard";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Failed to copy to clipboard: {ex.Message}");
        }
    }

    public async Task OpenFileOnStartupAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return;

        try
        {
            var content = await _fileService.ReadFileAsync(filePath);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                InputDocument.Text = content;
                ErrorLineNumbers = [];
                CompilationErrors = [];
                _hasUnsavedChanges = false;
                SetCurrentFilePath(filePath);
                this.RaisePropertyChanged(nameof(HasErrors));
                this.RaisePropertyChanged(nameof(ErrorSummary));
                this.RaisePropertyChanged(nameof(OutputBorderBrush));
            });

            _recentFilesService.AddRecentFile(filePath);
            RefreshRecentFiles();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Failed to open file: {ex.Message}");
        }
    }

    private async Task OpenRecentFileAsync(string filePath)
    {
        if (!await CheckUnsavedChangesAsync())
            return;

        try
        {
            if (!File.Exists(filePath))
            {
                await _dialogService.ShowMessageDialogAsync("Error", "File no longer exists.");
                _recentFilesService.AddRecentFile(filePath);
                RefreshRecentFiles();
                return;
            }

            var content = await _fileService.ReadFileAsync(filePath);
            var fileName = _fileService.GetFileName(filePath);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                InputDocument.Text = content;
                ErrorLineNumbers = [];
                CompilationErrors = [];
                _hasUnsavedChanges = false;
                SetCurrentFilePath(filePath);
                this.RaisePropertyChanged(nameof(HasErrors));
                this.RaisePropertyChanged(nameof(ErrorSummary));
                this.RaisePropertyChanged(nameof(OutputBorderBrush));
            });

            _recentFilesService.AddRecentFile(filePath);
            RefreshRecentFiles();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("Error", $"Failed to open file: {ex.Message}");
        }
    }

    private void RefreshRecentFiles()
    {
        RecentFiles = _recentFilesService.GetRecentFiles().ToList();
    }

    private async Task<bool> CheckUnsavedChangesAsync()
    {
        if (!_hasUnsavedChanges)
            return true;

        var result = await _dialogService.ShowConfirmationDialogAsync(
            "Unsaved Changes",
            "You have unsaved changes. Do you want to save before continuing?");

        if (result)
        {
            await SaveFileAsync();
        }

        return true;
    }

    private void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
        this.RaisePropertyChanged(nameof(HasOpenFile));
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var fileName = string.IsNullOrEmpty(_currentFilePath)
            ? "Untitled"
            : _fileService.GetFileName(_currentFilePath);

        var unsavedMarker = _hasUnsavedChanges ? "*" : "";
        WindowTitle = $"CodeDesignerLite - {fileName}{unsavedMarker}";
    }

    private async Task ShowAboutAsync()
    {
        var version = $"{CDL_Version:f2}";
        var message = $"Code Designer Lite is based on Code Designer by Gtlcpimp.\nCreated by harry62 through anger and frustration using Gemini.\n\nNew Commands:\nfloat $100 // create float value at address\nb :label // simple branch, converts to beq zero, zero, :label\n\n\nVersion: {version}";

        await _dialogService.ShowMessageDialogAsync("About Code Designer Lite", message);
    }

    private void Exit()
    {
        Environment.Exit(0);
    }
}
