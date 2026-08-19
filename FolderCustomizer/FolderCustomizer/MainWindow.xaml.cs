using FolderCustomizer.Editor;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderCustomizer.Services;

using DrawingColor = System.Drawing.Color;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using MediaColor = System.Windows.Media.Color;

namespace FolderCustomizer;

public partial class MainWindow : Window
{
    private const int IconSize = 264;

    private const uint ShcneUpdateItem = 0x00002000;
    private const uint ShcnfPathW = 0x0005;

    private string? _folderPath;
    private Image? _folderIcon;
    private MediaColor? _selectedFolderColor;

    public MainWindow()
    {
        InitializeComponent();
        InitializeFolderIcon();
        SetEditorEnabled(false);
    }

    private void InitializeFolderIcon()
    {
        BitmapSource source =
            WindowsFolderIconProvider.GetDefaultFolderIcon();

        _folderIcon = new Image
        {
            Width = IconSize,
            Height = IconSize,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
            Source = source
        };

        iconEditorCanvas.Children.Insert(
            0,
            _folderIcon);
    }

    // ---------------------------------------------------------------------
    // Folder selection
    // ---------------------------------------------------------------------

    private void Btn_Load_Click(
    object sender,
    RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to customize"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _folderPath = dialog.FolderName;

        txt_SelectedFolder.Text = _folderPath;

        SetEditorEnabled(true);
    }

    private void Btn_ResetColour_Click(
    object sender,
    RoutedEventArgs e)
    {
        _selectedFolderColor = null;

        folderColourPreview.Background =
            Brushes.Transparent;

        folderColourPreview.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(209, 209, 209));

        txt_SelectedColour.Text =
            "Default";

        UpdateBaseImage();
    }

    // ---------------------------------------------------------------------
    // Overlay images
    // ---------------------------------------------------------------------

    private void Btn_AddImage_Click(object sender, RoutedEventArgs e)
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

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var editableImage = new EditableImageCanvas(
                new Uri(dialog.FileName, UriKind.Absolute));

            iconEditorCanvas.Children.Add(editableImage);
        }
        catch (Exception ex)
        {
            ShowError(
                "Couldn't add image",
                $"The selected image couldn't be loaded.\n\n{ex.Message}");
        }
    }

    // ---------------------------------------------------------------------
    // Folder colour
    // ---------------------------------------------------------------------

    private void Btn_ColourPicker_Click(
    object sender,
    RoutedEventArgs e)
    {
        using var dialog = new FormsColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        if (_selectedFolderColor is MediaColor currentColor)
        {
            dialog.Color = DrawingColor.FromArgb(
                currentColor.A,
                currentColor.R,
                currentColor.G,
                currentColor.B);
        }

        if (dialog.ShowDialog() !=
            System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _selectedFolderColor =
            ToWpfColor(dialog.Color);

        UpdateColourPreview(
            _selectedFolderColor.Value);

        UpdateBaseImage();
    }

    private void UpdateColourPreview(
    MediaColor color)
    {
        folderColourPreview.Background =
            new SolidColorBrush(color);

        folderColourPreview.BorderBrush =
            new SolidColorBrush(
                GetPreviewBorderColor(color));

        txt_SelectedColour.Text =
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static MediaColor GetPreviewBorderColor(
    MediaColor color)
    {
        const double darkenFactor = 0.78;

        return MediaColor.FromArgb(
            color.A,
            (byte)(color.R * darkenFactor),
            (byte)(color.G * darkenFactor),
            (byte)(color.B * darkenFactor));
    }

    private void UpdateBaseImage()
    {
        if (_folderIcon is null)
            return;

        BitmapSource source =
            WindowsFolderIconProvider
                .GetDefaultFolderIcon();

        if (_selectedFolderColor is MediaColor colour)
        {
            source =
                ApplyColor(
                    source,
                    colour);
        }

        _folderIcon.Source = source;
    }
    private static MediaColor ToWpfColor(DrawingColor color)
    {
        return MediaColor.FromArgb(
            color.A,
            color.R,
            color.G,
            color.B);
    }

    private static WriteableBitmap ApplyColor(
        BitmapSource source,
        MediaColor color)
    {
        var bitmap = new WriteableBitmap(source);

        bitmap.Lock();

        try
        {
            unsafe
            {
                byte* buffer = (byte*)bitmap.BackBuffer.ToPointer();

                int stride = bitmap.BackBufferStride;
                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;

                for (int y = 0; y < height; y++)
                {
                    byte* row = buffer + (y * stride);

                    for (int x = 0; x < width; x++)
                    {
                        byte* pixel = row + (x * 4);

                        byte blue = pixel[0];
                        byte green = pixel[1];
                        byte red = pixel[2];

                        double intensity =
                            ((0.299 * red) +
                             (0.587 * green) +
                             (0.114 * blue))
                            / 255.0;

                        pixel[0] = (byte)(color.B * intensity);
                        pixel[1] = (byte)(color.G * intensity);
                        pixel[2] = (byte)(color.R * intensity);
                    }
                }
            }

            bitmap.AddDirtyRect(
                new Int32Rect(
                    0,
                    0,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight));
        }
        finally
        {
            bitmap.Unlock();
        }

        bitmap.Freeze();

        return bitmap;
    }

    // ---------------------------------------------------------------------
    // Apply folder icon
    // ---------------------------------------------------------------------

    private void Btn_UpdateFolder_Icon(
    object sender,
    RoutedEventArgs e)
    {
        if (!TryGetSelectedFolder(out string folderPath))
            return;

        try
        {
            string pngPath =
                Path.Combine(
                    folderPath,
                    "custom_icon.png");

            string icoPath =
                Path.Combine(
                    folderPath,
                    "custom_icon.ico");

            RenderEditorToPng(pngPath);

            if (File.Exists(icoPath))
                File.Delete(icoPath);

            ImagingHelper.ConvertToIcon(
                pngPath,
                icoPath);

            // The PNG is only an intermediate file.
            File.Delete(pngPath);

            ApplyIconToFolder(
                folderPath,
                icoPath);

            MessageBox.Show(
                $"Folder icon updated successfully!\n\n" +
                $"Note: You may need to refresh the folder view or restart Explorer to see the changes.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RefreshShell(folderPath);

            ResetEditorAfterSave();
        }
        catch (UnauthorizedAccessException)
        {
            ShowError(
                "Permission denied",
                "Folder Customizer doesn't have permission to modify this folder.");
        }
        catch (IOException ex)
        {
            ShowError(
                "Couldn't update folder",
                ex.Message);
        }
        catch (Exception ex)
        {
            ShowError(
                "Something went wrong",
                ex.Message);
        }
    }

    private void ResetEditorAfterSave()
    {
        _folderPath = null;

        txt_SelectedFolder.Text =
            "No folder selected";

        _selectedFolderColor = null;

        txt_SelectedColour.Text =
            "Default";

        folderColourPreview.Background =
            Brushes.Transparent;

        folderColourPreview.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(204, 204, 204));

        // Remove all overlays but keep the base folder icon.
        for (int i = iconEditorCanvas.Children.Count - 1;
             i >= 0;
             i--)
        {
            if (iconEditorCanvas.Children[i]
                is EditableImageCanvas)
            {
                iconEditorCanvas.Children.RemoveAt(i);
            }
        }

        UpdateBaseImage();

        SetEditorEnabled(false);
    }

    private bool TryGetSelectedFolder(out string folderPath)
    {
        folderPath = _folderPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ShowError(
                "No folder selected",
                "Choose a folder before applying the icon.");

            return false;
        }

        if (!Directory.Exists(folderPath))
        {
            ShowError(
                "Folder not found",
                "The selected folder no longer exists.");

            return false;
        }

        return true;
    }

    private void RenderEditorToPng(string outputPath)
    {
        int width = (int)Math.Ceiling(iconEditorCanvas.ActualWidth);
        int height = (int)Math.Ceiling(iconEditorCanvas.ActualHeight);

        if (width <= 0 || height <= 0)
        {
            width = IconSize;
            height = IconSize;
        }

        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);

        bitmap.Render(iconEditorCanvas);

        var encoder = new PngBitmapEncoder();

        encoder.Frames.Add(
            BitmapFrame.Create(bitmap));

        using FileStream stream = File.Create(outputPath);

        encoder.Save(stream);
    }

    private static void ApplyIconToFolder(
        string folderPath,
        string iconPath)
    {
        string desktopIniPath = Path.Combine(
            folderPath,
            "desktop.ini");

        // Explorer expects the folder to have the System attribute
        // for desktop.ini customizations.
        FileAttributes folderAttributes =
            File.GetAttributes(folderPath);

        File.SetAttributes(
            folderPath,
            folderAttributes | FileAttributes.System);

        if (File.Exists(desktopIniPath))
        {
            File.SetAttributes(
                desktopIniPath,
                FileAttributes.Normal);

            File.Delete(desktopIniPath);
        }

        string desktopIni =
            "[.ShellClassInfo]\r\n" +
            $"IconResource={Path.GetFileName(iconPath)},0\r\n";

        File.WriteAllText(
            desktopIniPath,
            desktopIni,
            Encoding.Unicode);

        File.SetAttributes(
            desktopIniPath,
            FileAttributes.Hidden |
            FileAttributes.System);

        File.SetAttributes(
            iconPath,
            File.GetAttributes(iconPath) |
            FileAttributes.Hidden);
    }

    // ---------------------------------------------------------------------
    // Shell refresh
    // ---------------------------------------------------------------------

    private static void RefreshShell(string folderPath)
    {
        SHChangeNotify(
            ShcneUpdateItem,
            ShcnfPathW,
            folderPath,
            null);
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(
        uint eventId,
        uint flags,
        string? item1,
        string? item2);

    // ---------------------------------------------------------------------
    // UI helpers
    // ---------------------------------------------------------------------

    private void ShowError(
        string title,
        string message)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void SetEditorEnabled(bool enabled)
    {
        editorWorkspace.IsEnabled = enabled;

        btn_addImage.IsEnabled = enabled;
        btn_ColourPicker.IsEnabled = enabled;
        btn_ResetColour.IsEnabled = enabled;
        btn_ApplyToolbar.IsEnabled = enabled;
    }
}