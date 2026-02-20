using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeDesignerLite.Core.Compiler;
using CodeDesignerLite.Core.Services;
using CodeDesignerLite.Desktop.Services;
using CodeDesignerLite.Desktop.ViewModels;
using CodeDesignerLite.Desktop.Views;
using System.Linq;
using System.Text;

namespace CodeDesignerLite.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var compiler = new MipsCompiler();
            var fileService = new FileService();
            var dialogService = new DialogService();
            var clipboardService = new ClipboardService();
            var recentFilesService = new RecentFilesService();
            var viewModel = new MainWindowViewModel(compiler, fileService, dialogService, clipboardService, recentFilesService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            var fileArg = desktop.Args?.FirstOrDefault(a => !a.StartsWith('-'));
            if (!string.IsNullOrEmpty(fileArg))
            {
                desktop.MainWindow.Opened += async (_, _) =>
                    await viewModel.OpenFileOnStartupAsync(fileArg);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
