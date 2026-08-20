using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace FolderCustomizer.Services;

public sealed class ColorPickerService
{
    public MediaColor? PickColor(MediaColor? currentColor = null)
    {
        using var dialog =
            new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                AnyColor = true
            };

        if (currentColor is MediaColor color)
        {
            dialog.Color =
                DrawingColor.FromArgb(
                    color.A,
                    color.R,
                    color.G,
                    color.B);
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return null;
        }

        return MediaColor.FromArgb(
            dialog.Color.A,
            dialog.Color.R,
            dialog.Color.G,
            dialog.Color.B);
    }
}