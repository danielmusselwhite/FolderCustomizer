using FolderCustomizer.Editor;
using FolderCustomizer.Services;
using FolderCustomizer.ViewModels;
using System;
using System.Collections.Specialized;
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
    private readonly ImageProcessingService _imageProcessingService;

    private Image? _folderIcon;

    public MainWindow()
    {
        InitializeComponent();

        _folderIconService = new FolderIconService();
        _imageProcessingService = new ImageProcessingService();
        _viewModel = new MainViewModel(
            new FolderPickerService(),
            new ColorPickerService(),
            _folderIconService,
            _imageProcessingService,
            new ImagePickerService());

        DataContext = _viewModel;

        InitializeFolderIcon();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.OverlayImages.CollectionChanged += OverlayImages_CollectionChanged;
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

    #region Overlay Images Event Handling

    private void OverlayImages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            ClearOverlays();
            return;
        }

        if (e.NewItems is null)
            return;

        foreach (EditorImageViewModel imageViewModel in e.NewItems)
            AddOverlay(imageViewModel);
    }

    private void AddOverlay(EditorImageViewModel imageViewModel)
    {
        try
        {
            var editableImage = new EditableImageCanvas(new Uri(imageViewModel.ImagePath, UriKind.Absolute));
            iconEditorCanvas.Children.Add(editableImage);
        }
        catch (Exception ex)
        {
            ShowError("Couldn't add image", $"The selected image couldn't be loaded.\n\n{ex.Message}");
        }
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
            source = _imageProcessingService.ApplyColor(source, colour);

        _folderIcon.Source = source;
    }

    #endregion

    #region Overlay Images

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