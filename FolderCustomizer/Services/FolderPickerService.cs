using Microsoft.Win32;

namespace FolderCustomizer.Services;

public sealed class FolderPickerService
{
    public string? SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to customize"
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }
}