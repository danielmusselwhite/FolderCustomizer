using CommunityToolkit.Mvvm.ComponentModel;
using FolderCustomizer.Editor;
using System;

namespace FolderCustomizer.ViewModels;

/// <summary>
/// Represents an image overlay placed on top of the folder icon within the editor.
/// </summary>
/// <remarks>
/// Each instance stores the editable visual state of a single overlay image, including
/// its position, dimensions, rotation, and selection state.
///
/// The view model also exposes callbacks that allow the overlay to request selection
/// or removal without directly depending on the parent <see cref="MainViewModel"/>.
/// </remarks>
public partial class EditorImageViewModel : ObservableObject
{
    #region Observable State

    /// <summary>
    /// Gets or sets the crop shape applied to the overlay image.
    /// </summary>
    [ObservableProperty]
    private ImageCropShape cropShape;

    /// <summary>
    /// Gets or sets the file path of the image displayed by this overlay.
    /// </summary>
    [ObservableProperty]
    private string imagePath;

    /// <summary>
    /// Gets or sets the horizontal position of the overlay within the editor canvas.
    /// </summary>
    [ObservableProperty]
    private double x;

    /// <summary>
    /// Gets or sets the vertical position of the overlay within the editor canvas.
    /// </summary>
    [ObservableProperty]
    private double y;

    /// <summary>
    /// Gets or sets the rendered width of the overlay.
    /// </summary>
    [ObservableProperty]
    private double width;

    /// <summary>
    /// Gets or sets the rendered height of the overlay.
    /// </summary>
    [ObservableProperty]
    private double height;

    /// <summary>
    /// Gets or sets the clockwise rotation of the overlay, in degrees.
    /// </summary>
    [ObservableProperty]
    private double rotation;

    /// <summary>
    /// Gets or sets whether this overlay is currently selected in the editor.
    /// </summary>
    /// <remarks>
    /// Selection state can be used by the view to display editing controls such as
    /// resize handles, rotation handles, or a selection border.
    /// </remarks>
    [ObservableProperty]
    private bool isSelected;

    #endregion

    #region Actions

    /// <summary>
    /// Gets or sets the callback invoked when this overlay requests to become selected.
    /// </summary>
    /// <remarks>
    /// The owning view model assigns this callback when the overlay is added to the editor,
    /// allowing the overlay to participate in selection without directly referencing its parent.
    /// </remarks>
    public Action<EditorImageViewModel>? SelectAction { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when this overlay requests to be removed.
    /// </summary>
    /// <remarks>
    /// The owning view model assigns this callback so the overlay can request deletion
    /// while collection management remains the responsibility of the parent.
    /// </remarks>
    public Action<EditorImageViewModel>? DeleteAction { get; set; }

    /// <summary>
    /// Gets or sets the action invoked when this overlay requests deselection.
    /// </summary>
    public Action<EditorImageViewModel>? DeselectAction { get; set; }

    #endregion

    #region Construction

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorImageViewModel"/> class.
    /// </summary>
    /// <param name="imagePath">
    /// The path of the image file represented by the overlay.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="imagePath"/> is <see langword="null"/>, empty,
    /// or consists only of white-space characters.
    /// </exception>
    public EditorImageViewModel(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        ImagePath = imagePath;
    }

    #endregion
}