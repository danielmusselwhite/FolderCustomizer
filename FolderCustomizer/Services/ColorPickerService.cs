using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace FolderCustomizer.Services;

/// <summary>
/// Provides colour selection for the folder icon editor.
/// </summary>
/// <remarks>
/// This service wraps the Windows Forms colour picker and converts between
/// <see cref="System.Drawing.Color"/>, used by the dialog, and
/// <see cref="System.Windows.Media.Color"/>, used by the WPF application.
/// </remarks>
public sealed class ColorPickerService
{
    /// <summary>
    /// Opens a colour-selection dialog and allows the user to choose a colour.
    /// </summary>
    /// <param name="currentColor">
    /// The currently selected WPF colour, if one exists. When supplied, this colour
    /// is used as the initial selection displayed by the colour picker.
    /// </param>
    /// <returns>
    /// The selected colour as a <see cref="MediaColor"/>, or <see langword="null"/>
    /// if the user cancels or closes the dialog without confirming a selection.
    /// </returns>
    public MediaColor? PickColor(MediaColor? currentColor = null)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        if (currentColor is MediaColor color)
        {
            // ColorDialog uses System.Drawing.Color, so convert the WPF colour
            // before using it as the dialog's initial selection.
            dialog.Color = DrawingColor.FromArgb(
                color.A,
                color.R,
                color.G,
                color.B);
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return null;

        // Convert the Windows Forms result back to the colour type used by WPF.
        return MediaColor.FromArgb(
            dialog.Color.A,
            dialog.Color.R,
            dialog.Color.G,
            dialog.Color.B);
    }
}