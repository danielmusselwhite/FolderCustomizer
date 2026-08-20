using Microsoft.Win32;

namespace FolderCustomizer.Services;

public sealed class ImagePickerService
{
    public string? SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add overlay image",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|" +
                     "PNG images (*.png)|*.png|" +
                     "JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}