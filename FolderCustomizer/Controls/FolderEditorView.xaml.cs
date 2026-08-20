using FolderCustomizer.Editor;
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

namespace FolderCustomizer.Views.Controls;

public partial class FolderEditorView : UserControl
{
    private const int IconSize = 256;

    public FolderEditorView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    #region ViewModel Integration

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
            oldViewModel.RenderRequested -= RenderEditorAsync;

        if (e.NewValue is MainViewModel newViewModel)
            newViewModel.RenderRequested += RenderEditorAsync;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.RenderRequested -= RenderEditorAsync;
    }

    #endregion

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
        if (DataContext is not MainViewModel viewModel)
            yield break;

        foreach (EditorImageViewModel imageViewModel in viewModel.OverlayImages)
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