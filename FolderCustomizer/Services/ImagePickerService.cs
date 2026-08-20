using Microsoft.Win32;

namespace FolderCustomizer.Services;

/// <summary>
/// Provides image file selection for overlay images used by the folder icon editor.
/// </summary>
/// <remarks>
/// This service encapsulates the WPF file-selection dialog so that image selection
/// remains separate from the editor's presentation and state-management logic.
/// </remarks>
public sealed class ImagePickerService
{
    /// <summary>
    /// Opens a file-selection dialog and allows the user to choose an image
    /// to add as an editor overlay.
    /// </summary>
    /// <returns>
    /// The full path of the selected image file, or <see langword="null"/> if
    /// the user cancels or closes the dialog without selecting a file.
    /// </returns>
    /// <remarks>
    /// The picker currently accepts PNG and JPEG images and restricts selection
    /// to a single existing file.
    /// </remarks>
    public string? SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add overlay image",
            Filter =
                "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|" +
                "PNG images (*.png)|*.png|" +
                "JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}