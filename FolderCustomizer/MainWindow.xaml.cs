using FolderCustomizer.Editor;
using FolderCustomizer.Services;
using FolderCustomizer.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        _viewModel.RenderRequested += RenderEditorAsync;
    }

    #region Rendering

    private Task RenderEditorAsync(string outputPath)
    {
        RenderEditorToPng(outputPath);
        return Task.CompletedTask;
    }

    private void RenderEditorToPng(string outputPath)
    {
        List<EditableImageCanvas> editableImages = GetEditableImages().ToList();

        try
        {
            foreach (EditableImageCanvas editableImage in editableImages)
                editableImage.HideEditorChrome();

            iconEditorSurface.UpdateLayout();

            int width = (int)Math.Ceiling(iconEditorSurface.ActualWidth);
            int height = (int)Math.Ceiling(iconEditorSurface.ActualHeight);

            if (width <= 0 || height <= 0)
            {
                width = IconSize;
                height = IconSize;
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(iconEditorSurface);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using FileStream stream = File.Create(outputPath);
            encoder.Save(stream);
        }
        finally
        {
            foreach (EditableImageCanvas editableImage in editableImages)
                editableImage.RestoreEditorChrome();

            iconEditorSurface.UpdateLayout();
        }
    }

    private IEnumerable<EditableImageCanvas> GetEditableImages()
    {
        foreach (EditorImageViewModel imageViewModel in _viewModel.OverlayImages)
        {
            if (overlayItemsControl.ItemContainerGenerator.ContainerFromItem(imageViewModel) is not ContentPresenter presenter)
                continue;

            EditableImageCanvas? editableImage = FindVisualChild<EditableImageCanvas>(presenter);

            if (editableImage is not null)
                yield return editableImage;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T match)
                return match;

            T? descendant = FindVisualChild<T>(child);

            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    #endregion
}