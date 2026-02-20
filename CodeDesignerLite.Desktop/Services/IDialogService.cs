using System.Threading.Tasks;

namespace CodeDesignerLite.Desktop.Services
{
    public interface IDialogService
    {
        Task<string?> ShowOpenFileDialogAsync();
        Task<string?> ShowSaveFileDialogAsync();
        Task ShowMessageDialogAsync(string title, string message);
        Task<bool> ShowConfirmationDialogAsync(string title, string message);
    }
}
