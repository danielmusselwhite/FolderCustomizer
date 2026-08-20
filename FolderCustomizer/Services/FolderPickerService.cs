using Microsoft.Win32;

namespace FolderCustomizer.Services;

/// <summary>
/// Provides folder selection for the folder icon editor.
/// </summary>
/// <remarks>
/// This service encapsulates the WPF folder-selection dialog so that folder
/// selection remains separate from the editor's presentation and state-management
/// logic.
/// </remarks>
public sealed class FolderPickerService
{
    /// <summary>
    /// Opens a folder-selection dialog and allows the user to choose a folder
    /// to customise.
    /// </summary>
    /// <returns>
    /// The full path of the selected folder, or <see langword="null"/> if the
    /// user cancels or closes the dialog without selecting a folder.
    /// </returns>
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