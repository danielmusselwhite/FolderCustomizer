using FolderCustomizer.Editor;
using FolderCustomizer.Services;
using FolderCustomizer.ViewModels;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer;

public partial class MainWindow : Window
{
    private const int IconSize = 256;

    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            new FolderPickerService(),
            new ColorPickerService(),
            new FolderIconService(),
            new ImageProcessingService(),
            new ImagePickerService());

        DataContext = _viewModel;

        _viewModel.OverlayImages.CollectionChanged += OverlayImages_CollectionChanged;
        _viewModel.RenderRequested += RenderEditorAsync;
    }

    #region Overlay Images Event Handling

    private void OverlayImages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            ClearOverlays();
            return;
        }

        if (e.NewItems is not null)
        {
            foreach (EditorImageViewModel image in e.NewItems)
                AddOverlay(image);
        }

        if (e.OldItems is not null)
        {
            foreach (EditorImageViewModel image in e.OldItems)
                RemoveOverlay(image);
        }
    }

    private void AddOverlay(EditorImageViewModel imageViewModel)
    {
        try
        {
            var editableImage = new EditableImageCanvas(imageViewModel);

            editableImage.DeleteRequested += (_, _) =>
            {
                _viewModel.OverlayImages.Remove(imageViewModel);
            };

            iconEditorCanvas.Children.Add(editableImage);
        }
        catch (Exception ex)
        {
            ShowError("Couldn't add image", $"The selected image couldn't be loaded.\n\n{ex.Message}");
        }
    }

    private void RemoveOverlay(EditorImageViewModel imageViewModel)
    {
        for (int i = iconEditorCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (iconEditorCanvas.Children[i] is EditableImageCanvas editableImage &&
                ReferenceEquals(editableImage.ViewModel, imageViewModel))
            {
                iconEditorCanvas.Children.RemoveAt(i);
                return;
            }
        }
    }

    #endregion

    #region Render Editor
    private async Task RenderEditorAsync(string outputPath)
    {
        await RenderEditorToPng(outputPath);
        await Task.CompletedTask;
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

    #region Apply Icon

    private async Task RenderEditorToPng(string outputPath)
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