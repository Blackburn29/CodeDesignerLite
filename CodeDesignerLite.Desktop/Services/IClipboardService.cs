using System.Threading.Tasks;

namespace CodeDesignerLite.Desktop.Services
{
    public interface IClipboardService
    {
        Task SetTextAsync(string text);
    }
}
