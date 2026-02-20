using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace CodeDesignerLite.Desktop.Services
{
    public class DialogService : IDialogService
    {
        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public async Task<string?> ShowOpenFileDialogAsync()
        {
            var window = GetMainWindow();
            if (window == null)
                return null;

            var storageProvider = window.StorageProvider;
            if (storageProvider == null)
                return null;

            var fileTypes = new FilePickerFileType[]
            {
                new FilePickerFileType("MIPS Assembly Files")
                {
                    Patterns = ["*.txt", "*.cds", "*.asm", "*.s"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            };

            var options = new FilePickerOpenOptions
            {
                Title = "Open Assembly File",
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            };

            var result = await storageProvider.OpenFilePickerAsync(options);
            if (result != null && result.Count > 0)
            {
                return result[0].Path.LocalPath;
            }

            return null;
        }

        public async Task<string?> ShowSaveFileDialogAsync()
        {
            var window = GetMainWindow();
            if (window == null)
                return null;

            var storageProvider = window.StorageProvider;
            if (storageProvider == null)
                return null;

            var fileTypes = new FilePickerFileType[]
            {
                new FilePickerFileType("MIPS Assembly Files")
                {
                    Patterns = ["*.txt", "*.cds", "*.asm", "*.s"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            };

            var options = new FilePickerSaveOptions
            {
                Title = "Save Assembly File",
                FileTypeChoices = fileTypes,
                DefaultExtension = "txt"
            };

            var result = await storageProvider.SaveFilePickerAsync(options);
            if (result != null)
            {
                return result.Path.LocalPath;
            }

            return null;
        }

        public async Task ShowMessageDialogAsync(string title, string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                title,
                message,
                ButtonEnum.Ok,
                Icon.Info);

            await messageBox.ShowAsync();
        }

        public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                title,
                message,
                ButtonEnum.YesNo,
                Icon.Warning);

            var result = await messageBox.ShowAsync();
            return result == ButtonResult.Yes;
        }
    }
}
