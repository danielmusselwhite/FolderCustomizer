using FolderCustomizer.Editor;
using FolderCustomizer.Services;
using FolderCustomizer.ViewModels;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MediaColor = System.Windows.Media.Color;

namespace FolderCustomizer;

public partial class MainWindow : Window
{
    private const int IconSize = 256;

    private readonly MainViewModel _viewModel;
    private readonly FolderIconService _folderIconService;

    private Image? _folderIcon;

    public MainWindow()
    {
        InitializeComponent();

        _folderIconService = new FolderIconService();
        _viewModel = new MainViewModel(new FolderPickerService(), new ColorPickerService(), _folderIconService);

        DataContext = _viewModel;

        InitializeFolderIcon();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    #region ViewModel Event Handling

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.SelectedFolderPath):
                OnSelectedFolderChanged();
                break;

            case nameof(MainViewModel.SelectedColour):
                OnSelectedColourChanged();
                break;
        }
    }

    private void OnSelectedFolderChanged()
    {
        ClearOverlays();
        UpdateBaseImage();
        UpdateColourPreview();
    }

    private void OnSelectedColourChanged()
    {
        UpdateBaseImage();
        UpdateColourPreview();
    }

    #endregion

    #region Folder Icon

    private void InitializeFolderIcon()
    {
        BitmapSource source = WindowsFolderIconProvider.GetDefaultFolderIcon();

        _folderIcon = new Image
        {
            Width = IconSize,
            Height = IconSize,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
            Source = source
        };

        iconEditorCanvas.Children.Insert(0, _folderIcon);
    }

    private void UpdateBaseImage()
    {
        if (_folderIcon is null)
            return;

        BitmapSource source = !string.IsNullOrWhiteSpace(_viewModel.SelectedFolderPath)
            ? _folderIconService.GetFolderIcon(_viewModel.SelectedFolderPath)
            : WindowsFolderIconProvider.GetDefaultFolderIcon();

        if (_viewModel.SelectedColour is MediaColor colour)
            source = ApplyColor(source, colour);

        _folderIcon.Source = source;
    }

    #endregion

    #region Overlay Images

    private void Btn_AddImage_Click(object sender, RoutedEventArgs e)
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

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var editableImage = new EditableImageCanvas(new Uri(dialog.FileName, UriKind.Absolute));
            iconEditorCanvas.Children.Add(editableImage);
        }
        catch (Exception ex)
        {
            ShowError("Couldn't add image", $"The selected image couldn't be loaded.\n\n{ex.Message}");
        }
    }

    private void ClearOverlays()
    {
        for (int i = iconEditorCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (iconEditorCanvas.Children[i] is EditableImageCanvas)
                iconEditorCanvas.Children.RemoveAt(i);
        }
    }

    #endregion

    #region Colour

    private void UpdateColourPreview()
    {
        if (_viewModel.SelectedColour is not MediaColor colour)
        {
            folderColourPreview.Background = Brushes.Transparent;
            folderColourPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            return;
        }

        folderColourPreview.Background = new SolidColorBrush(colour);
        folderColourPreview.BorderBrush = new SolidColorBrush(GetPreviewBorderColor(colour));
    }

    private static MediaColor GetPreviewBorderColor(MediaColor color)
    {
        const double darkenFactor = 0.78;

        return MediaColor.FromArgb(
            color.A,
            (byte)(color.R * darkenFactor),
            (byte)(color.G * darkenFactor),
            (byte)(color.B * darkenFactor));
    }

    private static WriteableBitmap ApplyColor(BitmapSource source, MediaColor color)
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

                        double intensity = ((0.299 * red) + (0.587 * green) + (0.114 * blue)) / 255.0;

                        pixel[0] = (byte)(color.B * intensity);
                        pixel[1] = (byte)(color.G * intensity);
                        pixel[2] = (byte)(color.R * intensity);
                    }
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        }
        finally
        {
            bitmap.Unlock();
        }

        bitmap.Freeze();

        return bitmap;
    }

    #endregion

    #region Apply Icon

    private void Btn_UpdateFolder_Icon(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedFolder(out string folderPath))
            return;

        try
        {
            string pngPath = Path.Combine(folderPath, "custom_icon.png");
            string icoPath = Path.Combine(folderPath, "custom_icon.ico");

            RenderEditorToPng(pngPath);

            if (File.Exists(icoPath))
            {
                File.SetAttributes(icoPath, FileAttributes.Normal);
                File.Delete(icoPath);
            }

            ImagingHelper.ConvertToIcon(pngPath, icoPath, 256);

            if (File.Exists(pngPath))
                File.Delete(pngPath);

            _folderIconService.ApplyCustomIcon(folderPath, icoPath);

            ResetEditorAfterSave();

            MessageBox.Show(
                this,
                "The folder icon was applied successfully.",
                "Icon applied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (UnauthorizedAccessException)
        {
            ShowError("Permission denied", "Folder Customizer doesn't have permission to modify this folder.");
        }
        catch (IOException ex)
        {
            ShowError("Couldn't update folder", ex.Message);
        }
        catch (Exception ex)
        {
            ShowError("Something went wrong", ex.Message);
        }
    }

    private bool TryGetSelectedFolder(out string folderPath)
    {
        folderPath = _viewModel.SelectedFolderPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ShowError("No folder selected", "Choose a folder before applying the icon.");
            return false;
        }

        if (!Directory.Exists(folderPath))
        {
            ShowError("Folder not found", "The selected folder no longer exists.");
            return false;
        }

        return true;
    }

    private void RenderEditorToPng(string outputPath)
    {
        var editableImages = iconEditorCanvas.Children
            .OfType<EditableImageCanvas>()
            .ToList();

        try
        {
            foreach (EditableImageCanvas editableImage in editableImages)
                editableImage.HideEditorChrome();

            iconEditorCanvas.UpdateLayout();

            int width = (int)Math.Ceiling(iconEditorCanvas.ActualWidth);
            int height = (int)Math.Ceiling(iconEditorCanvas.ActualHeight);

            if (width <= 0 || height <= 0)
            {
                width = IconSize;
                height = IconSize;
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(iconEditorCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using FileStream stream = File.Create(outputPath);
            encoder.Save(stream);
        }
        finally
        {
            foreach (EditableImageCanvas editableImage in editableImages)
                editableImage.RestoreEditorChrome();

            iconEditorCanvas.UpdateLayout();
        }
    }

    #endregion

    #region Clear Style

    private void Btn_ClearStyle_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedFolder(out string folderPath))
            return;

        try
        {
            _folderIconService.ClearCustomStyle(folderPath);

            ResetEditorAfterSave();

            MessageBox.Show(
                this,
                "The custom folder style was removed successfully.",
                "Style removed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (UnauthorizedAccessException)
        {
            ShowError("Permission denied", "Folder Customizer doesn't have permission to clear the custom icon from this folder.");
        }
        catch (IOException ex)
        {
            ShowError("Couldn't clear style", ex.Message);
        }
        catch (Exception ex)
        {
            ShowError("Something went wrong", ex.Message);
        }
    }

    #endregion

    #region Reset

    private void ResetEditorAfterSave()
    {
        _viewModel.SelectedFolderPath = null;
        _viewModel.SelectedColour = null;
        _viewModel.SelectedColourText = "Default";
        _viewModel.IsEditorEnabled = false;
        _viewModel.HasExistingStyle = false;

        folderColourPreview.Background = Brushes.Transparent;
        folderColourPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));

        ClearOverlays();
        UpdateBaseImage();
    }

    #endregion

    #region UI Helpers

    private void ShowError(string title, string message)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    #endregion
}