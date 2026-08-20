using FolderCustomizer.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FolderCustomizer.Editor;

/// <summary>
/// Provides an interactive editing surface for a single image overlay within the
/// folder icon editor.
/// </summary>
/// <remarks>
/// <para>
/// The control renders an image together with editor-only chrome for selection,
/// resizing, and rotation. Users can drag the image, resize it from any corner,
/// rotate it using a dedicated rotation handle, nudge it with the keyboard, or
/// remove it from the composition.
/// </para>
///
/// <para>
/// Visual state such as position, dimensions, rotation, and selection is synchronised
/// with an associated <see cref="EditorImageViewModel"/>. This allows the parent editor
/// to retain the overlay state independently of the control's visual representation.
/// </para>
///
/// <para>
/// The control is created programmatically rather than through XAML because its visual
/// elements and interaction handles form a tightly coupled editing surface. Editor
/// chrome can be temporarily suppressed when the parent
/// <see cref="Views.Controls.FolderEditorView"/> renders the final icon.
/// </para>
/// </remarks>
public sealed class EditableImageCanvas : Canvas
{
    #region Constants

    /// <summary>
    /// Maximum initial dimension, in pixels, assigned to newly added overlay images.
    /// </summary>
    private const double DefaultSize = 180;

    /// <summary>
    /// Minimum width or height, in pixels, to which an overlay can be resized.
    /// </summary>
    private const double MinimumSize = 24;

    /// <summary>
    /// Width and height, in pixels, of each resize handle.
    /// </summary>
    private const double HandleSize = 12;

    /// <summary>
    /// Additional hit-test area surrounding each manipulation handle.
    /// </summary>
    /// <remarks>
    /// The visible handles remain small while the larger invisible hit area makes
    /// them easier to acquire with the mouse.
    /// </remarks>
    private const double HandleHitPadding = 6;

    /// <summary>
    /// Diameter, in pixels, of the rotation handle.
    /// </summary>
    private const double RotationHandleSize = 14;

    /// <summary>
    /// Distance, in pixels, that the rotation handle extends above the overlay.
    /// </summary>
    private const double RotationHandleOffset = 30;

    /// <summary>
    /// Primary colour used by selected editor chrome.
    /// </summary>
    private static readonly Brush AccentBrush =
        new SolidColorBrush(Color.FromRgb(24, 90, 189));

    /// <summary>
    /// Translucent colour used when an unselected overlay is hovered.
    /// </summary>
    private static readonly Brush HoverBrush =
        new SolidColorBrush(Color.FromArgb(150, 24, 90, 189));

    #endregion

    #region Fields

    // Associated editor state.
    private EditorImageViewModel _viewModel = null!;

    // Core visual elements.
    private Image _image = null!;
    private Border _selectionBorder = null!;

    // Corner resize handles.
    private Rectangle _topLeftHandle = null!;
    private Rectangle _topRightHandle = null!;
    private Rectangle _bottomLeftHandle = null!;
    private Rectangle _bottomRightHandle = null!;

    // Rotation controls and transform.
    private Line _rotationLine = null!;
    private Ellipse _rotationHandle = null!;
    private RotateTransform _rotationTransform = null!;

    // Parent editor surface used for coordinate calculations and deselection.
    private Canvas? _editorCanvas;

    // Current pointer interaction.
    private InteractionMode _interactionMode;
    private ResizeCorner _activeResizeCorner;

    // Visual/editor lifecycle state.
    private bool _isInitialized;
    private bool _isHovered;
    private bool _isEditorChromeSuppressed;

    // State captured when a drag begins.
    private Point _dragStartMouse;
    private double _dragStartLeft;
    private double _dragStartTop;

    // State captured when a resize begins.
    private Point _resizeStartMouse;
    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    // State captured when a rotation begins.
    private Point _rotationCentre;
    private double _rotationStartAngle;
    private double _rotationStartMouseAngle;

    #endregion

    #region Public API

    /// <summary>
    /// Gets the view model containing the editable state represented by this control.
    /// </summary>
    public EditorImageViewModel ViewModel => _viewModel;

    /// <summary>
    /// Gets the current clockwise rotation of the overlay, in degrees.
    /// </summary>
    public double Rotation => _rotationTransform.Angle;

    /// <summary>
    /// Temporarily hides all editor-only visual elements.
    /// </summary>
    /// <remarks>
    /// This is used while rendering the final folder icon so that selection borders,
    /// resize handles, and rotation controls are not included in the output image.
    /// Call <see cref="RestoreEditorChrome"/> when rendering is complete.
    /// </remarks>
    public void HideEditorChrome()
    {
        _isEditorChromeSuppressed = true;
        UpdateChrome();
    }

    /// <summary>
    /// Restores editor-only visual elements after they have been temporarily hidden.
    /// </summary>
    public void RestoreEditorChrome()
    {
        _isEditorChromeSuppressed = false;
        UpdateChrome();
    }

    #endregion

    #region Construction

    /// <summary>
    /// Initializes a new instance of the <see cref="EditableImageCanvas"/> class.
    /// </summary>
    /// <remarks>
    /// Full visual initialisation is deferred until the control is loaded because its
    /// <see cref="FrameworkElement.DataContext"/> must contain an
    /// <see cref="EditorImageViewModel"/>.
    /// </remarks>
    public EditableImageCanvas()
    {
        Focusable = true;
        ClipToBounds = false;
        Background = Brushes.Transparent;

        // Rotate around the visual centre of the overlay rather than its top-left corner.
        RenderTransformOrigin = new Point(0.5, 0.5);

        Loaded += OnControlLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the control when it first enters the visual tree.
    /// </summary>
    private void OnControlLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_isInitialized)
            return;

        if (DataContext is not EditorImageViewModel viewModel)
        {
            throw new InvalidOperationException(
                "EditableImageCanvas requires an EditorImageViewModel DataContext.");
        }

        Initialize(viewModel);

        _isInitialized = true;
    }

    /// <summary>
    /// Creates the overlay visuals and connects the control to its view model and
    /// parent editor.
    /// </summary>
    /// <param name="viewModel">
    /// The view model containing the overlay's editable state.
    /// </param>
    private void Initialize(EditorImageViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewModel.ImagePath);

        _viewModel = viewModel;

        BitmapImage bitmap =
            LoadBitmap(new Uri(viewModel.ImagePath, UriKind.Absolute));

        InitializeViewModelSize(bitmap);

        Width = viewModel.Width;
        Height = viewModel.Height;

        _rotationTransform =
            new RotateTransform(viewModel.Rotation);

        RenderTransform = _rotationTransform;

        // Build the image and all editor-only manipulation visuals.
        _image = CreateImage(bitmap);
        _selectionBorder = CreateSelectionBorder();

        _topLeftHandle = CreateResizeHandle(Cursors.SizeNWSE);
        _topRightHandle = CreateResizeHandle(Cursors.SizeNESW);
        _bottomLeftHandle = CreateResizeHandle(Cursors.SizeNESW);
        _bottomRightHandle = CreateResizeHandle(Cursors.SizeNWSE);

        _rotationLine = CreateRotationLine();
        _rotationHandle = CreateRotationHandle();

        AddVisuals();
        RegisterInteractionEvents();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Locate the ItemsControl's Canvas so pointer coordinates can be calculated
        // relative to the complete 256x256 editing surface.
        _editorCanvas = GetEditorCanvas();

        if (_editorCanvas is not null)
        {
            _editorCanvas.PreviewMouseLeftButtonDown +=
                OnEditorPreviewMouseLeftButtonDown;
        }

        UpdateChrome();
        UpdateImageClip();
    }

    /// <summary>
    /// Assigns initial overlay dimensions when the view model does not already contain
    /// a valid size.
    /// </summary>
    private void InitializeViewModelSize(BitmapSource bitmap)
    {
        if (_viewModel.Width > 0 && _viewModel.Height > 0)
            return;

        Size size = CalculateInitialSize(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            DefaultSize);

        _viewModel.Width = size.Width;
        _viewModel.Height = size.Height;
    }

    /// <summary>
    /// Adds the image and manipulation chrome to the canvas in rendering order.
    /// </summary>
    private void AddVisuals()
    {
        Children.Add(_image);
        Children.Add(_selectionBorder);
        Children.Add(_rotationLine);

        Children.Add(_topLeftHandle);
        Children.Add(_topRightHandle);
        Children.Add(_bottomLeftHandle);
        Children.Add(_bottomRightHandle);

        Children.Add(_rotationHandle);
    }

    /// <summary>
    /// Registers pointer and keyboard events used to manipulate the overlay.
    /// </summary>
    private void RegisterInteractionEvents()
    {
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        MouseMove += OnMouseMove;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Disconnects external event subscriptions when the control leaves the visual tree.
    /// </summary>
    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (!_isInitialized)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (_editorCanvas is not null)
        {
            _editorCanvas.PreviewMouseLeftButtonDown -=
                OnEditorPreviewMouseLeftButtonDown;
        }

        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    #endregion

    #region Visual Creation

    /// <summary>
    /// Loads an overlay image into memory from the supplied URI.
    /// </summary>
    /// <remarks>
    /// <see cref="BitmapCacheOption.OnLoad"/> ensures that the source file is no longer
    /// required after loading, preventing the editor from keeping the image file locked.
    /// </remarks>
    private static BitmapImage LoadBitmap(Uri imagePath)
    {
        var bitmap = new BitmapImage();

        bitmap.BeginInit();
        bitmap.UriSource = imagePath;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        bitmap.Freeze();

        return bitmap;
    }

    private static Image CreateImage(BitmapSource bitmap)
    {
        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };
    }

    private static Border CreateSelectionBorder()
    {
        return new Border
        {
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(1.5),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private static Rectangle CreateResizeHandle(Cursor cursor)
    {
        return new Rectangle
        {
            Width = HandleSize,
            Height = HandleSize,
            RadiusX = 2,
            RadiusY = 2,
            Fill = Brushes.White,
            Stroke = AccentBrush,
            StrokeThickness = 2,
            Cursor = cursor,
            Visibility = Visibility.Collapsed
        };
    }

    private static Line CreateRotationLine()
    {
        return new Line
        {
            Stroke = AccentBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private static Ellipse CreateRotationHandle()
    {
        return new Ellipse
        {
            Width = RotationHandleSize,
            Height = RotationHandleSize,
            Fill = Brushes.White,
            Stroke = AccentBrush,
            StrokeThickness = 2,
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed
        };
    }

    /// <summary>
    /// Calculates an overlay's initial dimensions while retaining the source image's
    /// aspect ratio.
    /// </summary>
    private static Size CalculateInitialSize(
        int pixelWidth,
        int pixelHeight,
        double maximumDimension)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return new Size(maximumDimension, maximumDimension);

        double scale = Math.Min(
            maximumDimension / pixelWidth,
            maximumDimension / pixelHeight);

        // Do not enlarge source images beyond their native dimensions.
        scale = Math.Min(scale, 1);

        return new Size(
            Math.Max(MinimumSize, pixelWidth * scale),
            Math.Max(MinimumSize, pixelHeight * scale));
    }

    #endregion

    #region ViewModel Events

    /// <summary>
    /// Responds to changes in the bound overlay ViewModel and updates visual state.
    /// </summary>
    /// <param name="sender">The ViewModel that raised the change notification.</param>
    /// <param name="e">Information about the changed property.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorImageViewModel.IsSelected))
            UpdateChrome();

        if (e.PropertyName == nameof(EditorImageViewModel.CropShape))
            UpdateImageClip();
    }

    #endregion

    #region Parent Interaction

    /// <summary>
    /// Deselects this overlay when the user clicks elsewhere on the parent editor canvas.
    /// </summary>
    private void OnEditorPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_viewModel.IsSelected)
            return;

        // Clicks originating from this control belong to the current overlay and
        // should not cause it to be deselected.
        if (e.OriginalSource is not DependencyObject source ||
            !IsDescendantOf(source, this))
        {
            Deselect();
        }
    }

    private static bool IsDescendantOf(
        DependencyObject element,
        DependencyObject ancestor)
    {
        DependencyObject? current = element;

        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    #endregion

    #region Mouse Interaction

    private void OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        _isHovered = true;

        UpdateCursor(e);
        UpdateChrome();
    }

    private void OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        // Mouse capture means an active drag/resize/rotation may legitimately move
        // the pointer outside the control's normal bounds.
        if (IsMouseCaptured)
            return;

        _isHovered = false;
        Cursor = Cursors.Arrow;

        UpdateChrome();
    }

    /// <summary>
    /// Selects the overlay and begins the manipulation represented by the pointer
    /// location.
    /// </summary>
    private void OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        Select();

        ResizeCorner? corner =
            GetResizeCornerAtMouse(e);

        // Manipulation handles take priority over dragging the image itself.
        if (corner is not null)
            BeginResize(corner.Value, e);
        else if (IsMouseOverRotationHandle(e))
            BeginRotation(e);
        else
            BeginDrag(e);

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_interactionMode == InteractionMode.None)
            return;

        _interactionMode = InteractionMode.None;

        if (IsMouseCaptured)
            ReleaseMouseCapture();

        UpdateChrome();

        e.Handled = true;
    }

    private void OnMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!IsMouseCaptured)
        {
            UpdateCursor(e);
            return;
        }

        switch (_interactionMode)
        {
            case InteractionMode.Dragging:
                Drag(e);
                break;

            case InteractionMode.Resizing:
                Resize(e);
                break;

            case InteractionMode.Rotating:
                Rotate(e);
                break;
        }
    }

    #endregion

    #region Selection

    /// <summary>
    /// Requests exclusive selection of this overlay and gives it keyboard focus.
    /// </summary>
    private void Select()
    {
        _viewModel.SelectAction?.Invoke(_viewModel);

        Focus();
        Keyboard.Focus(this);

        UpdateChrome();
    }

    /// <summary>
    /// Ends any active interaction and requests deselection of this overlay.
    /// </summary>
    private void Deselect()
    {
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        _interactionMode = InteractionMode.None;

        _viewModel.DeselectAction?.Invoke(_viewModel);

        UpdateChrome();
    }

    #endregion

    #region Cropping(Clipping)
    /// <summary>
    /// Updates the non-destructive crop or opacity mask applied to the overlay image
    /// based on the currently selected crop preset.
    /// </summary>
    private void UpdateImageClip()
    {
        if (!_isInitialized)
            return;

        double width = GetCurrentWidth();
        double height = GetCurrentHeight();

        if (width <= 0 || height <= 0)
            return;

        _image.Clip = null;
        _image.OpacityMask = null;

        switch (_viewModel.CropShape)
        {
            case ImageCropShape.RoundedRectangle:
                _image.Clip = CreateRoundedRectangleClip(width, height);
                break;

            case ImageCropShape.Circle:
                _image.Clip = CreateCircleClip(width, height);
                break;

            case ImageCropShape.Folder:
                _image.OpacityMask = CreateFolderMask(includeTab: true);
                break;

            case ImageCropShape.FolderWithoutTab:
                _image.OpacityMask = CreateFolderMask(includeTab: false);
                break;
        }
    }

    /// <summary>
    /// Creates a rounded-rectangle clipping geometry for the current image bounds.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The rounded clipping geometry.</returns>
    private static Geometry CreateRoundedRectangleClip(double width, double height)
    {
        double radius = Math.Min(width, height) * 0.12;

        return new RectangleGeometry(
            new Rect(0, 0, width, height),
            radius,
            radius);
    }

    /// <summary>
    /// Creates an elliptical clipping geometry constrained to the current image bounds.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The elliptical clipping geometry.</returns>
    private static Geometry CreateCircleClip(double width, double height)
    {
        return new EllipseGeometry(
            new Point(width / 2, height / 2),
            width / 2,
            height / 2);
    }

    /// <summary>
    /// Creates an opacity mask matching the folder silhouette used by the editor.
    /// </summary>
    /// <param name="includeTab">
    /// True to use the complete folder silhouette including its upper tab;
    /// otherwise false to use only the main folder body.
    /// </param>
    /// <returns>An image brush suitable for use as an opacity mask.</returns>
    private static Brush CreateFolderMask(bool includeTab)
    {
        string resourcePath = includeTab
            ? "pack://application:,,,/assets/masks/Folder.png"
            : "pack://application:,,,/assets/masks/FolderWithoutTab.png";

        var bitmap = new BitmapImage();

        bitmap.BeginInit();
        bitmap.UriSource = new Uri(resourcePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };

        brush.Freeze();

        return brush;
    }
    #endregion

    #region Dragging

    /// <summary>
    /// Begins a drag operation and captures the initial pointer and overlay position.
    /// </summary>
    private void BeginDrag(MouseButtonEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        _interactionMode = InteractionMode.Dragging;

        _dragStartMouse = e.GetPosition(canvas);
        _dragStartLeft = _viewModel.X;
        _dragStartTop = _viewModel.Y;

        Cursor = Cursors.SizeAll;

        // Mouse capture allows dragging to continue when the pointer leaves the
        // overlay's visual bounds.
        CaptureMouse();
    }

    /// <summary>
    /// Moves the overlay according to the pointer displacement from the drag origin.
    /// </summary>
    private void Drag(MouseEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        Point mouse = e.GetPosition(canvas);
        Vector movement = mouse - _dragStartMouse;

        // Clamp the unrotated overlay bounds to the editor surface.
        double maxLeft =
            Math.Max(0, canvas.ActualWidth - Width);

        double maxTop =
            Math.Max(0, canvas.ActualHeight - Height);

        double left = Math.Clamp(
            _dragStartLeft + movement.X,
            0,
            maxLeft);

        double top = Math.Clamp(
            _dragStartTop + movement.Y,
            0,
            maxTop);

        SetPosition(left, top);
    }

    #endregion

    #region Resizing

    /// <summary>
    /// Begins resizing from the specified corner and captures the initial geometry.
    /// </summary>
    private void BeginResize(
        ResizeCorner corner,
        MouseButtonEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        _interactionMode = InteractionMode.Resizing;
        _activeResizeCorner = corner;

        _resizeStartMouse = e.GetPosition(canvas);
        _resizeStartLeft = _viewModel.X;
        _resizeStartTop = _viewModel.Y;
        _resizeStartWidth = Width;
        _resizeStartHeight = Height;

        CaptureMouse();

        UpdateCursorForCorner(corner);
    }

    /// <summary>
    /// Resizes the overlay according to the current pointer position and keyboard
    /// modifiers.
    /// </summary>
    /// <remarks>
    /// Holding <kbd>Ctrl</kbd> resizes around the centre of the overlay.
    /// Holding <kbd>Shift</kbd> preserves its original aspect ratio.
    /// </remarks>
    private void Resize(MouseEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        Point currentMouse =
            e.GetPosition(canvas);

        Vector screenDelta =
            currentMouse - _resizeStartMouse;

        // Pointer movement is measured in canvas coordinates. Rotate that vector back
        // into the overlay's local coordinate system so resize directions remain
        // intuitive even when the image itself is rotated.
        Vector delta =
            RotateVector(
                screenDelta,
                -_rotationTransform.Angle);

        bool fromCentre =
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        bool preserveAspectRatio =
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        bool leftCorner =
            _activeResizeCorner is
                ResizeCorner.TopLeft or
                ResizeCorner.BottomLeft;

        bool topCorner =
            _activeResizeCorner is
                ResizeCorner.TopLeft or
                ResizeCorner.TopRight;

        double widthDelta =
            delta.X * (leftCorner ? -1 : 1);

        double heightDelta =
            delta.Y * (topCorner ? -1 : 1);

        // Centre-based resizing moves both opposing edges, so the size changes
        // by twice the pointer displacement.
        if (fromCentre)
        {
            widthDelta *= 2;
            heightDelta *= 2;
        }

        double newWidth =
            Math.Max(
                MinimumSize,
                _resizeStartWidth + widthDelta);

        double newHeight =
            Math.Max(
                MinimumSize,
                _resizeStartHeight + heightDelta);

        if (preserveAspectRatio)
            ApplyAspectRatio(ref newWidth, ref newHeight);

        ApplyResize(
            newWidth,
            newHeight,
            leftCorner,
            topCorner,
            fromCentre,
            canvas);

        UpdateChrome();
        UpdateImageClip();
    }

    /// <summary>
    /// Adjusts proposed dimensions so that they retain the aspect ratio present when
    /// the resize operation began.
    /// </summary>
    private void ApplyAspectRatio(
        ref double width,
        ref double height)
    {
        double scaleX =
            width / _resizeStartWidth;

        double scaleY =
            height / _resizeStartHeight;

        // Use whichever axis has moved furthest from its starting scale as the
        // controlling dimension.
        double scale =
            Math.Abs(scaleX - 1) > Math.Abs(scaleY - 1)
                ? scaleX
                : scaleY;

        double minimumScale = Math.Max(
            MinimumSize / _resizeStartWidth,
            MinimumSize / _resizeStartHeight);

        scale =
            Math.Max(minimumScale, scale);

        width = _resizeStartWidth * scale;
        height = _resizeStartHeight * scale;
    }

    /// <summary>
    /// Applies calculated resize dimensions while constraining the overlay to the
    /// parent editing surface.
    /// </summary>
    private void ApplyResize(
        double newWidth,
        double newHeight,
        bool leftCorner,
        bool topCorner,
        bool fromCentre,
        Canvas parent)
    {
        double newLeft;
        double newTop;

        if (fromCentre)
        {
            // Preserve the original centre point while both opposing edges move.
            double centreX =
                _resizeStartLeft + (_resizeStartWidth / 2);

            double centreY =
                _resizeStartTop + (_resizeStartHeight / 2);

            // The closest editor boundary determines how far the overlay can expand
            // symmetrically around its centre.
            double maxWidth = Math.Max(
                MinimumSize,
                2 * Math.Min(
                    centreX,
                    parent.ActualWidth - centreX));

            double maxHeight = Math.Max(
                MinimumSize,
                2 * Math.Min(
                    centreY,
                    parent.ActualHeight - centreY));

            newWidth =
                Math.Clamp(
                    newWidth,
                    MinimumSize,
                    maxWidth);

            newHeight =
                Math.Clamp(
                    newHeight,
                    MinimumSize,
                    maxHeight);

            newLeft = centreX - (newWidth / 2);
            newTop = centreY - (newHeight / 2);
        }
        else
        {
            // When resizing from a corner, the opposite edges remain anchored.
            double startRight =
                _resizeStartLeft + _resizeStartWidth;

            double startBottom =
                _resizeStartTop + _resizeStartHeight;

            if (leftCorner)
            {
                newWidth =
                    Math.Min(newWidth, startRight);

                newLeft =
                    startRight - newWidth;
            }
            else
            {
                newLeft =
                    _resizeStartLeft;

                newWidth =
                    Math.Min(
                        newWidth,
                        parent.ActualWidth - newLeft);
            }

            if (topCorner)
            {
                newHeight =
                    Math.Min(newHeight, startBottom);

                newTop =
                    startBottom - newHeight;
            }
            else
            {
                newTop =
                    _resizeStartTop;

                newHeight =
                    Math.Min(
                        newHeight,
                        parent.ActualHeight - newTop);
            }
        }

        Width = Math.Max(MinimumSize, newWidth);
        Height = Math.Max(MinimumSize, newHeight);

        SetPosition(
            Math.Max(0, newLeft),
            Math.Max(0, newTop));

        SyncSizeToViewModel();
    }

    #endregion

    #region Rotation

    /// <summary>
    /// Begins rotating the overlay around its visual centre.
    /// </summary>
    private void BeginRotation(MouseButtonEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        _interactionMode = InteractionMode.Rotating;

        _rotationCentre = new Point(
            _viewModel.X + (Width / 2),
            _viewModel.Y + (Height / 2));

        Point mouse =
            e.GetPosition(canvas);

        _rotationStartMouseAngle =
            CalculateAngle(_rotationCentre, mouse);

        _rotationStartAngle =
            _rotationTransform.Angle;

        Cursor = Cursors.Hand;

        CaptureMouse();
    }

    /// <summary>
    /// Updates the overlay rotation according to the pointer's angular movement.
    /// </summary>
    /// <remarks>
    /// Holding <kbd>Shift</kbd> snaps rotation to 15-degree increments.
    /// </remarks>
    private void Rotate(MouseEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        Point mouse =
            e.GetPosition(canvas);

        double currentMouseAngle =
            CalculateAngle(_rotationCentre, mouse);

        double angle =
            _rotationStartAngle +
            currentMouseAngle -
            _rotationStartMouseAngle;

        // Shift provides predictable angular snapping for precise alignment.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            angle = Math.Round(angle / 15) * 15;

        angle = NormalizeAngle(angle);

        _rotationTransform.Angle = angle;
        _viewModel.Rotation = angle;
    }

    /// <summary>
    /// Calculates the angle from a centre point to another point in degrees.
    /// </summary>
    private static double CalculateAngle(
        Point centre,
        Point point)
    {
        double radians = Math.Atan2(
            point.Y - centre.Y,
            point.X - centre.X);

        return radians * 180 / Math.PI;
    }

    /// <summary>
    /// Normalises an angle to the range 0 through less than 360 degrees.
    /// </summary>
    private static double NormalizeAngle(double angle)
    {
        angle %= 360;

        if (angle < 0)
            angle += 360;

        return angle;
    }

    #endregion

    #region Keyboard Interaction

    /// <summary>
    /// Handles keyboard manipulation of the selected overlay.
    /// </summary>
    /// <remarks>
    /// Arrow keys move the overlay by one pixel. Holding <kbd>Shift</kbd> increases
    /// the movement to ten pixels. Delete and Backspace request removal of the overlay.
    /// </remarks>
    private void OnKeyDown(
        object sender,
        KeyEventArgs e)
    {
        double nudge =
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? 10
                : 1;

        switch (e.Key)
        {
            case Key.Delete:
            case Key.Back:
                RequestDelete();
                break;

            case Key.Left:
                Nudge(-nudge, 0);
                break;

            case Key.Right:
                Nudge(nudge, 0);
                break;

            case Key.Up:
                Nudge(0, -nudge);
                break;

            case Key.Down:
                Nudge(0, nudge);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Moves the overlay by the supplied offset while keeping it within the editor.
    /// </summary>
    private void Nudge(
        double x,
        double y)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        double left = Math.Clamp(
            _viewModel.X + x,
            0,
            Math.Max(0, canvas.ActualWidth - Width));

        double top = Math.Clamp(
            _viewModel.Y + y,
            0,
            Math.Max(0, canvas.ActualHeight - Height));

        SetPosition(left, top);
    }

    /// <summary>
    /// Requests that the owning view model remove this overlay.
    /// </summary>
    private void RequestDelete()
    {
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        _viewModel.DeleteAction?.Invoke(_viewModel);
    }

    #endregion

    #region Editor Chrome

    /// <summary>
    /// Updates editor chrome after WPF has arranged the control.
    /// </summary>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size result =
            base.ArrangeOverride(arrangeSize);

        if (_isInitialized)
        {
            UpdateChrome();
            UpdateImageClip();
        }

        return result;
    }

    /// <summary>
    /// Updates the image, selection border, resize handles, rotation control, and
    /// their visibility to match the current editor state.
    /// </summary>
    private void UpdateChrome()
    {
        if (!_isInitialized)
            return;

        double width = GetCurrentWidth();
        double height = GetCurrentHeight();

        UpdateImageAndBorder(width, height);
        PositionResizeHandles(width, height);
        PositionRotationHandle(width);

        // Rendering suppresses all editor-only visuals regardless of selection
        // or hover state.
        if (_isEditorChromeSuppressed)
        {
            SetChromeVisibility(Visibility.Collapsed);
            return;
        }

        UpdateSelectionBorder();

        Visibility handleVisibility =
            _viewModel.IsSelected
                ? Visibility.Visible
                : Visibility.Collapsed;

        SetHandleVisibility(handleVisibility);
    }

    private void UpdateImageAndBorder(
        double width,
        double height)
    {
        _image.Width = width;
        _image.Height = height;

        _selectionBorder.Width = width;
        _selectionBorder.Height = height;

        SetLeft(_image, 0);
        SetTop(_image, 0);

        SetLeft(_selectionBorder, 0);
        SetTop(_selectionBorder, 0);
    }

    private void PositionResizeHandles(
        double width,
        double height)
    {
        double halfHandle =
            HandleSize / 2;

        // Centre each handle directly over its corresponding image corner.
        PositionHandle(
            _topLeftHandle,
            -halfHandle,
            -halfHandle);

        PositionHandle(
            _topRightHandle,
            width - halfHandle,
            -halfHandle);

        PositionHandle(
            _bottomLeftHandle,
            -halfHandle,
            height - halfHandle);

        PositionHandle(
            _bottomRightHandle,
            width - halfHandle,
            height - halfHandle);
    }

    private void PositionRotationHandle(double width)
    {
        // Extend the rotation control vertically from the centre of the top edge.
        _rotationLine.X1 = width / 2;
        _rotationLine.Y1 = 0;
        _rotationLine.X2 = width / 2;
        _rotationLine.Y2 =
            -RotationHandleOffset +
            (RotationHandleSize / 2);

        SetLeft(
            _rotationHandle,
            (width / 2) - (RotationHandleSize / 2));

        SetTop(
            _rotationHandle,
            -RotationHandleOffset);
    }

    private void UpdateSelectionBorder()
    {
        if (_viewModel.IsSelected)
        {
            _selectionBorder.BorderBrush = AccentBrush;
            _selectionBorder.Opacity = 1;
            _selectionBorder.Visibility = Visibility.Visible;
            return;
        }

        if (_isHovered)
        {
            _selectionBorder.BorderBrush = HoverBrush;
            _selectionBorder.Opacity = 0.75;
            _selectionBorder.Visibility = Visibility.Visible;
            return;
        }

        _selectionBorder.Visibility =
            Visibility.Collapsed;
    }

    private void SetChromeVisibility(
        Visibility visibility)
    {
        _selectionBorder.Visibility = visibility;

        SetHandleVisibility(visibility);
    }

    private void SetHandleVisibility(
        Visibility visibility)
    {
        _topLeftHandle.Visibility = visibility;
        _topRightHandle.Visibility = visibility;
        _bottomLeftHandle.Visibility = visibility;
        _bottomRightHandle.Visibility = visibility;

        _rotationLine.Visibility = visibility;
        _rotationHandle.Visibility = visibility;
    }

    private static void PositionHandle(
        FrameworkElement handle,
        double left,
        double top)
    {
        SetLeft(handle, left);
        SetTop(handle, top);
    }

    #endregion

    #region Hit Testing and Cursor

    /// <summary>
    /// Determines whether the pointer is currently over one of the resize handles.
    /// </summary>
    private ResizeCorner? GetResizeCornerAtMouse(
        MouseEventArgs e)
    {
        if (!_viewModel.IsSelected)
            return null;

        Point position =
            e.GetPosition(this);

        if (IsInsideHandle(position, _topLeftHandle))
            return ResizeCorner.TopLeft;

        if (IsInsideHandle(position, _topRightHandle))
            return ResizeCorner.TopRight;

        if (IsInsideHandle(position, _bottomLeftHandle))
            return ResizeCorner.BottomLeft;

        if (IsInsideHandle(position, _bottomRightHandle))
            return ResizeCorner.BottomRight;

        return null;
    }

    private bool IsMouseOverRotationHandle(
        MouseEventArgs e)
    {
        return
            _viewModel.IsSelected &&
            IsInsideHandle(
                e.GetPosition(this),
                _rotationHandle);
    }

    /// <summary>
    /// Determines whether a point lies within the enlarged hit-test area surrounding
    /// a manipulation handle.
    /// </summary>
    private static bool IsInsideHandle(
        Point point,
        FrameworkElement handle)
    {
        double left = GetLeft(handle);
        double top = GetTop(handle);

        return
            point.X >= left - HandleHitPadding &&
            point.X <= left + handle.Width + HandleHitPadding &&
            point.Y >= top - HandleHitPadding &&
            point.Y <= top + handle.Height + HandleHitPadding;
    }

    private void UpdateCursor(MouseEventArgs e)
    {
        ResizeCorner? corner =
            GetResizeCornerAtMouse(e);

        if (corner is not null)
        {
            UpdateCursorForCorner(corner.Value);
            return;
        }

        Cursor =
            IsMouseOverRotationHandle(e)
                ? Cursors.Hand
                : Cursors.SizeAll;
    }

    private void UpdateCursorForCorner(
        ResizeCorner corner)
    {
        Cursor = corner switch
        {
            ResizeCorner.TopLeft or
            ResizeCorner.BottomRight
                => Cursors.SizeNWSE,

            ResizeCorner.TopRight or
            ResizeCorner.BottomLeft
                => Cursors.SizeNESW,

            _ => Cursors.Arrow
        };
    }

    #endregion

    #region ViewModel Synchronization

    /// <summary>
    /// Updates the overlay position stored by the view model.
    /// </summary>
    private void SetPosition(
        double left,
        double top)
    {
        _viewModel.X = left;
        _viewModel.Y = top;
    }

    /// <summary>
    /// Copies the control's current dimensions into the view model.
    /// </summary>
    private void SyncSizeToViewModel()
    {
        _viewModel.Width = Width;
        _viewModel.Height = Height;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Locates the parent canvas that represents the complete overlay editing surface.
    /// </summary>
    private Canvas? GetEditorCanvas()
    {
        DependencyObject? current =
            VisualTreeHelper.GetParent(this);

        while (current is not null)
        {
            if (current is Canvas canvas)
                return canvas;

            current =
                VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private double GetCurrentWidth() =>
        double.IsNaN(Width)
            ? ActualWidth
            : Width;

    private double GetCurrentHeight() =>
        double.IsNaN(Height)
            ? ActualHeight
            : Height;

    /// <summary>
    /// Rotates a two-dimensional vector by the specified number of degrees.
    /// </summary>
    /// <remarks>
    /// Resizing uses this helper to translate mouse movement from the editor's
    /// coordinate system into the coordinate system of a rotated overlay.
    /// </remarks>
    private static Vector RotateVector(
        Vector vector,
        double degrees)
    {
        double radians =
            degrees * Math.PI / 180;

        double cos =
            Math.Cos(radians);

        double sin =
            Math.Sin(radians);

        return new Vector(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos));
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// Identifies the pointer manipulation currently being performed.
    /// </summary>
    private enum InteractionMode
    {
        None,
        Dragging,
        Resizing,
        Rotating
    }

    /// <summary>
    /// Identifies the corner from which an overlay is being resized.
    /// </summary>
    private enum ResizeCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    #endregion
}