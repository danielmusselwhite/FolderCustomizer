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

/// <summary>
/// Provides the visual editing surface used to compose and render a custom folder icon.
/// </summary>
/// <remarks>
/// <para>
/// The control displays the base folder preview together with any editable overlay images
/// exposed by <see cref="MainViewModel"/>.
/// </para>
///
/// <para>
/// It also acts as the rendering bridge between the view model and WPF. Because the final
/// icon composition consists of live visual elements, the view subscribes to
/// <see cref="MainViewModel.RenderRequested"/> and renders the editor surface when the
/// view model requests an output image.
/// </para>
///
/// <para>
/// Editor-only chrome such as selection borders, resize handles, and rotation controls is
/// temporarily hidden during rendering so that those controls are not included in the
/// generated icon.
/// </para>
/// </remarks>
public partial class FolderEditorView : UserControl
{
    #region Constants

    /// <summary>
    /// Represents the standard fallback size, in pixels, used when the editor surface
    /// does not report valid rendered dimensions.
    /// </summary>
    private const int IconSize = 256;

    #endregion

    #region Construction

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderEditorView"/> class.
    /// </summary>
    /// <remarks>
    /// The control monitors data-context changes so that it can subscribe to the
    /// render event exposed by the active <see cref="MainViewModel"/>. The subscription
    /// is removed when the control is unloaded to avoid retaining the view unnecessarily.
    /// </remarks>
    public FolderEditorView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region ViewModel Integration

    /// <summary>
    /// Updates the render-event subscription when the control's data context changes.
    /// </summary>
    /// <param name="sender">
    /// The control whose data context changed.
    /// </param>
    /// <param name="e">
    /// Event data containing the previous and new data-context values.
    /// </param>
    /// <remarks>
    /// The previous view model is explicitly unsubscribed before subscribing to the new
    /// instance. This prevents duplicate render handlers and avoids keeping obsolete
    /// view models referenced by the control.
    /// </remarks>
    private void OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
            oldViewModel.RenderRequested -= RenderEditorAsync;

        if (e.NewValue is MainViewModel newViewModel)
            newViewModel.RenderRequested += RenderEditorAsync;
    }

    /// <summary>
    /// Removes the render-event subscription when the control leaves the visual tree.
    /// </summary>
    /// <param name="sender">
    /// The control being unloaded.
    /// </param>
    /// <param name="e">
    /// Event data associated with the unload operation.
    /// </param>
    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.RenderRequested -= RenderEditorAsync;
    }

    #endregion

    #region Editor Rendering

    /// <summary>
    /// Handles a render request from the view model.
    /// </summary>
    /// <param name="outputPath">
    /// The destination path where the rendered PNG image should be written.
    /// </param>
    /// <returns>
    /// A completed task after the editor surface has been rendered.
    /// </returns>
    /// <remarks>
    /// Rendering is currently performed synchronously on the WPF UI thread because
    /// <see cref="RenderTargetBitmap"/> must operate against the live visual tree.
    /// The task-based signature matches the asynchronous render callback expected by
    /// <see cref="MainViewModel"/>.
    /// </remarks>
    private Task RenderEditorAsync(string outputPath)
    {
        RenderEditorToPng(outputPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Renders the current editor composition to a 256×256 PNG file.
    /// If transformed overlay content extends beyond the editor boundaries,
    /// the overlay layer is temporarily scaled down just enough to fit.
    /// </summary>
    /// <param name="outputPath">The destination path of the PNG file to create.</param>
    /// <remarks>
    /// Scaling is only applied when required and is never greater than 1.0,
    /// preventing repeated save operations from progressively shrinking the icon.
    /// The temporary render transform is restored after the export completes.
    /// </remarks>
    private void RenderEditorToPng(string outputPath)
    {
        List<EditableImageCanvas> editableImages = GetEditableImages().ToList();

        Transform originalTransform = overlayItemsControl.RenderTransform;
        double scale = CalculateOverlayExportScale(editableImages);

        try
        {
            foreach (EditableImageCanvas editableImage in editableImages)
                editableImage.HideEditorChrome();

            if (scale < 1.0)
                overlayItemsControl.RenderTransform = new ScaleTransform(scale, scale);

            iconEditorSurface.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                IconSize,
                IconSize,
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(iconEditorSurface);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using FileStream stream = File.Create(outputPath);
            encoder.Save(stream);
        }
        finally
        {
            overlayItemsControl.RenderTransform = originalTransform;

            foreach (EditableImageCanvas editableImage in editableImages)
                editableImage.RestoreEditorChrome();

            iconEditorSurface.UpdateLayout();
        }
    }

    /// <summary>
    /// Calculates the scale required to keep every transformed overlay inside
    /// the 256×256 export surface.
    /// </summary>
    /// <param name="editableImages">The editable overlay controls currently displayed.</param>
    /// <returns>
    /// A scale between 0 and 1. A value of 1 means no export scaling is required.
    /// </returns>
    private double CalculateOverlayExportScale(IEnumerable<EditableImageCanvas> editableImages)
    {
        Rect? combinedBounds = null;

        foreach (EditableImageCanvas editableImage in editableImages)
        {
            GeneralTransform transform = editableImage.TransformToVisual(iconEditorSurface);

            Rect localBounds = new Rect(
                0,
                0,
                editableImage.ActualWidth,
                editableImage.ActualHeight);

            Rect transformedBounds = transform.TransformBounds(localBounds);

            combinedBounds = combinedBounds is null
                ? transformedBounds
                : Rect.Union(combinedBounds.Value, transformedBounds);
        }

        if (combinedBounds is null)
            return 1.0;

        Rect bounds = combinedBounds.Value;

        double requiredWidth = Math.Max(
            IconSize,
            Math.Max(bounds.Right, IconSize - bounds.Left));

        double requiredHeight = Math.Max(
            IconSize,
            Math.Max(bounds.Bottom, IconSize - bounds.Top));

        const double safeSize = IconSize - 2;

        double scaleX = safeSize / requiredWidth;
        double scaleY = safeSize / requiredHeight;

        return Math.Min(1.0, Math.Min(scaleX, scaleY));
    }

    /// <summary>
    /// Enumerates the instantiated editable image controls currently displayed
    /// on the editor surface.
    /// </summary>
    /// <returns>
    /// The <see cref="EditableImageCanvas"/> instances associated with the current
    /// overlay image view models.
    /// </returns>
    /// <remarks>
    /// The overlay collection contains view models rather than controls, so the method
    /// resolves each generated item container and searches its visual tree for the
    /// corresponding <see cref="EditableImageCanvas"/>.
    ///
    /// Items whose visual containers have not yet been generated are skipped.
    /// </remarks>
    private IEnumerable<EditableImageCanvas> GetEditableImages()
    {
        if (DataContext is not MainViewModel viewModel)
            yield break;

        foreach (EditorImageViewModel imageViewModel in viewModel.OverlayImages)
        {
            if (overlayItemsControl.ItemContainerGenerator
                    .ContainerFromItem(imageViewModel)
                is not ContentPresenter presenter)
            {
                continue;
            }

            EditableImageCanvas? editableImage =
                FindVisualChild<EditableImageCanvas>(presenter);

            if (editableImage is not null)
                yield return editableImage;
        }
    }

    #endregion

    #region Visual Tree Helpers

    /// <summary>
    /// Searches the visual descendants of an element for the first child of the
    /// requested type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of visual element to locate.
    /// </typeparam>
    /// <param name="parent">
    /// The visual-tree node from which the recursive search should begin.
    /// </param>
    /// <returns>
    /// The first matching descendant, or <see langword="null"/> when no matching
    /// element exists.
    /// </returns>
    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(parent, i);

            if (child is T match)
                return match;

            T? descendant =
                FindVisualChild<T>(child);

            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    #endregion
}