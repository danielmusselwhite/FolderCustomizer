using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderCustomizer.Editor;
using FolderCustomizer.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.ViewModels;

/// <summary>
/// Provides the presentation logic and editable state for FolderCustomizer's main window.
/// </summary>
/// <remarks>
/// The view model coordinates folder selection, colour customisation, overlay images,
/// preview generation, and applying or removing custom folder icons.
///
/// User-interface state is exposed through observable properties and commands generated
/// by the CommunityToolkit.Mvvm source generators. Platform-specific and image-processing
/// operations are delegated to dedicated services to keep the view model focused on
/// coordinating the editor workflow.
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// Provides folder selection functionality.
    /// </summary>
    private readonly FolderPickerService _folderPickerService;

    /// <summary>
    /// Provides colour selection functionality.
    /// </summary>
    private readonly ColorPickerService _colorPickerService;

    /// <summary>
    /// Manages reading, applying, and removing custom Windows folder icons.
    /// </summary>
    private readonly FolderIconService _folderIconService;

    /// <summary>
    /// Performs image transformations used when generating folder previews.
    /// </summary>
    private readonly ImageProcessingService _imageProcessingService;

    /// <summary>
    /// Provides image file selection functionality for editor overlays.
    /// </summary>
    private readonly ImagePickerService _imagePickerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="folderPickerService">
    /// Service used to allow the user to select a folder.
    /// </param>
    /// <param name="colorPickerService">
    /// Service used to allow the user to select a custom folder colour.
    /// </param>
    /// <param name="folderIconService">
    /// Service used to inspect, apply, and remove custom folder icons.
    /// </param>
    /// <param name="imageProcessingService">
    /// Service used to perform image transformations required by the editor.
    /// </param>
    /// <param name="imagePickerService">
    /// Service used to select images that can be placed over the folder icon.
    /// </param>
    public MainViewModel(
        FolderPickerService folderPickerService,
        ColorPickerService colorPickerService,
        FolderIconService folderIconService,
        ImageProcessingService imageProcessingService,
        ImagePickerService imagePickerService)
    {
        _folderPickerService = folderPickerService;
        _colorPickerService = colorPickerService;
        _folderIconService = folderIconService;
        _imageProcessingService = imageProcessingService;
        _imagePickerService = imagePickerService;

        RefreshFolderPreview();
    }

    #region Events

    /// <summary>
    /// Occurs when the current editor composition must be rendered to an image file.
    /// </summary>
    /// <remarks>
    /// The view handles the actual rendering because the final composition contains
    /// visual elements owned by WPF. The supplied string represents the destination
    /// path where the rendered PNG image should be written.
    /// </remarks>
    public event Func<string, Task>? RenderRequested;

    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the overlay image currently selected in the editor.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedOverlay))]
    private EditorImageViewModel? selectedOverlay;

    /// <summary>
    /// Gets whether an overlay image is currently selected.
    /// </summary>
    public bool HasSelectedOverlay => SelectedOverlay is not null;

    /// <summary>
    /// Gets or sets the path of the folder currently being customised.
    /// </summary>
    [ObservableProperty]
    private string? selectedFolderPath;

    /// <summary>
    /// Gets or sets whether the folder customisation controls are enabled.
    /// </summary>
    /// <remarks>
    /// The editor is enabled after the user has selected a valid folder and is disabled
    /// when the editor returns to its initial state.
    /// </remarks>
    [ObservableProperty]
    private bool isEditorEnabled;

    /// <summary>
    /// Gets or sets whether the selected folder already has a custom style applied.
    /// </summary>
    /// <remarks>
    /// This value can be used by the user interface to expose actions that are only
    /// relevant when an existing custom folder icon can be removed.
    /// </remarks>
    [ObservableProperty]
    private bool hasExistingStyle;

    /// <summary>
    /// Gets or sets the colour currently applied to the folder preview.
    /// </summary>
    /// <remarks>
    /// A value of <see langword="null"/> represents the folder's default colour.
    /// </remarks>
    [ObservableProperty]
    private Color? selectedColour;

    /// <summary>
    /// Gets or sets the display text representing the currently selected colour.
    /// </summary>
    /// <remarks>
    /// Custom colours are represented as hexadecimal RGB values, while the absence
    /// of a custom colour is represented by "Default".
    /// </remarks>
    [ObservableProperty]
    private string selectedColourText = "Default";

    /// <summary>
    /// Gets or sets the folder image displayed by the editor preview.
    /// </summary>
    [ObservableProperty]
    private BitmapSource? folderPreview;

    /// <summary>
    /// Gets or sets the brush used to display the selected colour in the colour preview.
    /// </summary>
    [ObservableProperty]
    private Brush colourPreviewBrush = Brushes.Transparent;

    /// <summary>
    /// Gets or sets the brush used to draw the border around the colour preview.
    /// </summary>
    [ObservableProperty]
    private Brush colourPreviewBorderBrush =
        new SolidColorBrush(Color.FromRgb(204, 204, 204));

    /// <summary>
    /// Gets the collection of images currently placed over the folder icon.
    /// </summary>
    /// <remarks>
    /// Each item contains the editable state of an overlay, including its selection
    /// state and any positioning or transformation information maintained by
    /// <see cref="EditorImageViewModel"/>.
    /// </remarks>
    public ObservableCollection<EditorImageViewModel> OverlayImages { get; } = [];

    #endregion

    #region Commands

    /// <summary>
    /// Applies no crop to the selected overlay.
    /// </summary>
    [RelayCommand]
    private void CropNone()
    {
        if (SelectedOverlay is not null)
            SelectedOverlay.CropShape = ImageCropShape.None;
    }

    /// <summary>
    /// Applies a rounded-rectangle crop to the selected overlay.
    /// </summary>
    [RelayCommand]
    private void CropRounded()
    {
        if (SelectedOverlay is not null)
            SelectedOverlay.CropShape = ImageCropShape.RoundedRectangle;
    }

    /// <summary>
    /// Applies a circular crop to the selected overlay.
    /// </summary>
    [RelayCommand]
    private void CropCircle()
    {
        if (SelectedOverlay is not null)
            SelectedOverlay.CropShape = ImageCropShape.Circle;
    }

    /// <summary>
    /// Applies a folder-shaped crop to the selected overlay.
    /// </summary>
    [RelayCommand]
    private void CropFolder()
    {
        if (SelectedOverlay is not null)
            SelectedOverlay.CropShape = ImageCropShape.Folder;
    }

    /// <summary>
    /// Opens the folder picker and loads the selected folder into the editor.
    /// </summary>
    /// <remarks>
    /// Selecting a new folder clears any existing overlays and colour selection,
    /// determines whether the folder already has a custom style, enables the editor,
    /// and refreshes the visual previews.
    ///
    /// Cancelling the folder picker leaves the current editor state unchanged.
    /// </remarks>
    [RelayCommand]
    private void SelectFolder()
    {
        string? folderPath = _folderPickerService.SelectFolder();

        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        OverlayImages.Clear();

        SelectedFolderPath = folderPath;
        SelectedColour = null;
        SelectedColourText = "Default";
        IsEditorEnabled = true;
        HasExistingStyle = _folderIconService.HasCustomStyle(folderPath);

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    /// <summary>
    /// Opens the colour picker and applies the selected colour to the folder preview.
    /// </summary>
    /// <remarks>
    /// The current colour is supplied to the picker so it can be used as the initial
    /// selection. Cancelling the picker leaves the current colour unchanged.
    /// </remarks>
    [RelayCommand]
    private void ChooseColour()
    {
        Color? colour = _colorPickerService.PickColor(SelectedColour);

        if (colour is null)
            return;

        SelectedColour = colour;
        SelectedColourText =
            $"#{colour.Value.R:X2}{colour.Value.G:X2}{colour.Value.B:X2}";

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    /// <summary>
    /// Applies the specified crop shape to the currently selected overlay image.
    /// </summary>
    /// <param name="cropShape">The crop shape to apply.</param>
    [RelayCommand]
    private void SetCropShape(ImageCropShape cropShape)
    {
        if (SelectedOverlay is null)
            return;

        SelectedOverlay.CropShape = cropShape;
    }

    /// <summary>
    /// Removes the selected colour and restores the default folder colour.
    /// </summary>
    [RelayCommand]
    private void ResetColour()
    {
        SelectedColour = null;
        SelectedColourText = "Default";

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    /// <summary>
    /// Removes the custom style currently applied to the selected folder.
    /// </summary>
    /// <remarks>
    /// After the custom style has been removed, the editor is returned to its
    /// initial state.
    /// </remarks>
    [RelayCommand]
    private void ClearStyle()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        _folderIconService.ClearCustomStyle(SelectedFolderPath);

        ResetEditor();
    }

    /// <summary>
    /// Opens the image picker and adds the selected image as a new editor overlay.
    /// </summary>
    /// <remarks>
    /// Newly added overlays are configured with callbacks for selection and deletion
    /// before being added to <see cref="OverlayImages"/>. The new overlay is
    /// automatically selected so it can be manipulated immediately.
    ///
    /// Cancelling the image picker leaves the existing overlays unchanged.
    /// </remarks>
    [RelayCommand]
    private void AddImage()
    {
        string? imagePath = _imagePickerService.SelectImage();

        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        var image = new EditorImageViewModel(imagePath)
        {
            SelectAction = SelectOverlay,
            DeleteAction = RemoveOverlay,
            DeselectAction = DeselectOverlay,
        };

        OverlayImages.Add(image);

        SelectOverlay(image);
    }

    /// <summary>
    /// Renders the current editor composition and applies it as the selected folder's
    /// custom Windows icon.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous render and icon application operation.
    /// </returns>
    /// <remarks>
    /// The editor composition is first rendered to a temporary PNG file through
    /// <see cref="RenderRequested"/>. The PNG is then converted to ICO format and
    /// applied to the selected folder through <see cref="FolderIconService"/>.
    ///
    /// Any existing generated ICO file is removed before conversion to ensure that
    /// the newly rendered icon replaces it. The temporary PNG file is deleted in a
    /// <see langword="finally"/> block so that it is cleaned up even if conversion
    /// or icon application fails.
    /// </remarks>
    [RelayCommand]
    private async Task ApplyIconAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        if (RenderRequested is null)
            return;

        string folderPath = SelectedFolderPath;
        string pngPath = Path.Combine(folderPath, "custom_icon.png");
        string icoPath = Path.Combine(folderPath, "custom_icon.ico");

        try
        {
            await RenderRequested.Invoke(pngPath);

            if (File.Exists(icoPath))
            {
                File.SetAttributes(icoPath, FileAttributes.Normal);
                File.Delete(icoPath);
            }

            ImagingHelper.ConvertToIcon(pngPath, icoPath);

            _folderIconService.ApplyCustomIcon(folderPath, icoPath);

            ResetEditor();
        }
        finally
        {
            if (File.Exists(pngPath))
                File.Delete(pngPath);
        }
    }

    #endregion

    #region Overlay Management

    /// <summary>
    /// Selects the specified overlay image and deselects all other overlays.
    /// </summary>
    /// <param name="selectedImage">
    /// The overlay image that should become the active selection.
    /// </param>
    /// <remarks>
    /// Reference equality is used so that only the exact
    /// <see cref="EditorImageViewModel"/> instance supplied to this method is marked
    /// as selected.
    /// </remarks>
    public void SelectOverlay(EditorImageViewModel selectedImage)
    {
        foreach (EditorImageViewModel image in OverlayImages)
            image.IsSelected = ReferenceEquals(image, selectedImage);

        SelectedOverlay = selectedImage;
    }

    /// <summary>
    /// Removes the specified overlay image from the editor.
    /// </summary>
    /// <param name="image">The overlay image to remove.</param>
    private void RemoveOverlay(EditorImageViewModel image)
    {
        OverlayImages.Remove(image);
    }

    /// <summary>
    /// Deselects the specified overlay if it is currently selected.
    /// </summary>
    /// <param name="image">The overlay requesting deselection.</param>
    private void DeselectOverlay(EditorImageViewModel image)
    {
        image.IsSelected = false;

        if (ReferenceEquals(SelectedOverlay, image))
            SelectedOverlay = null;
    }
    #endregion

    #region Preview

    /// <summary>
    /// Regenerates the folder image displayed by the editor.
    /// </summary>
    /// <remarks>
    /// If a folder is selected, its current folder icon is loaded. Otherwise, the
    /// standard Windows folder icon is used. When a custom colour has been selected,
    /// the colour transformation is applied before the resulting image is assigned
    /// to <see cref="FolderPreview"/>.
    /// </remarks>
    private void RefreshFolderPreview()
    {
        BitmapSource source = !string.IsNullOrWhiteSpace(SelectedFolderPath)
            ? _folderIconService.GetFolderIcon(SelectedFolderPath)
            : WindowsFolderIconProvider.GetDefaultFolderIcon();

        if (SelectedColour is Color colour)
            source = _imageProcessingService.ApplyColor(source, colour);

        FolderPreview = source;
    }

    /// <summary>
    /// Updates the colour swatch and border displayed by the editor.
    /// </summary>
    /// <remarks>
    /// When no custom colour is selected, the swatch is transparent and uses the
    /// editor's neutral default border. Custom colours use the selected colour as
    /// the fill and a darker variation of that colour as the border.
    /// </remarks>
    private void RefreshColourPreview()
    {
        if (SelectedColour is not Color colour)
        {
            ColourPreviewBrush = Brushes.Transparent;
            ColourPreviewBorderBrush =
                new SolidColorBrush(Color.FromRgb(204, 204, 204));
            return;
        }

        ColourPreviewBrush = new SolidColorBrush(colour);
        ColourPreviewBorderBrush =
            new SolidColorBrush(GetPreviewBorderColor(colour));
    }

    /// <summary>
    /// Produces a darker variation of a colour for use as a preview border.
    /// </summary>
    /// <param name="color">The source colour from which the border colour is derived.</param>
    /// <returns>
    /// A colour retaining the source alpha value with its red, green, and blue
    /// channels darkened.
    /// </returns>
    private static Color GetPreviewBorderColor(Color color)
    {
        const double darkenFactor = 0.78;

        return Color.FromArgb(
            color.A,
            (byte)(color.R * darkenFactor),
            (byte)(color.G * darkenFactor),
            (byte)(color.B * darkenFactor));
    }

    #endregion

    #region State

    /// <summary>
    /// Returns the editor to its initial state.
    /// </summary>
    /// <remarks>
    /// Clears all overlay images and folder-specific customisation state, disables
    /// the editor, and restores the default colour and folder previews.
    /// </remarks>
    private void ResetEditor()
    {
        OverlayImages.Clear();

        SelectedFolderPath = null;
        SelectedColour = null;
        SelectedColourText = "Default";
        IsEditorEnabled = false;
        HasExistingStyle = false;

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    #endregion
}