using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace CodeDesignerLite.Desktop.Services
{
    public class ClipboardService : IClipboardService
    {
        public async Task SetTextAsync(string text)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
        }
    }
}
