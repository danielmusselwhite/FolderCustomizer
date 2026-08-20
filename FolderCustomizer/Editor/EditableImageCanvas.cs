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

public sealed class EditableImageCanvas : Canvas
{
    #region Constants

    private const double DefaultSize = 180;
    private const double MinimumSize = 24;

    private const double HandleSize = 12;
    private const double HandleHitPadding = 6;

    private const double RotationHandleSize = 14;
    private const double RotationHandleOffset = 30;

    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(24, 90, 189));
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromArgb(150, 24, 90, 189));

    #endregion

    #region Fields

    private EditorImageViewModel _viewModel = null!;

    private Image _image = null!;
    private Border _selectionBorder = null!;

    private Rectangle _topLeftHandle = null!;
    private Rectangle _topRightHandle = null!;
    private Rectangle _bottomLeftHandle = null!;
    private Rectangle _bottomRightHandle = null!;

    private Line _rotationLine = null!;
    private Ellipse _rotationHandle = null!;
    private RotateTransform _rotationTransform = null!;

    private Canvas? _editorCanvas;

    private InteractionMode _interactionMode;
    private ResizeCorner _activeResizeCorner;

    private bool _isInitialized;
    private bool _isHovered;
    private bool _isEditorChromeSuppressed;

    private Point _dragStartMouse;
    private double _dragStartLeft;
    private double _dragStartTop;

    private Point _resizeStartMouse;
    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    private Point _rotationCentre;
    private double _rotationStartAngle;
    private double _rotationStartMouseAngle;

    #endregion

    #region Public API

    public EditorImageViewModel ViewModel => _viewModel;

    public double Rotation => _rotationTransform.Angle;

    public void HideEditorChrome()
    {
        _isEditorChromeSuppressed = true;
        UpdateChrome();
    }

    public void RestoreEditorChrome()
    {
        _isEditorChromeSuppressed = false;
        UpdateChrome();
    }

    #endregion

    #region Initialization

    public EditableImageCanvas()
    {
        Focusable = true;
        ClipToBounds = false;
        Background = Brushes.Transparent;
        RenderTransformOrigin = new Point(0.5, 0.5);

        Loaded += OnControlLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
            return;

        if (DataContext is not EditorImageViewModel viewModel)
            throw new InvalidOperationException("EditableImageCanvas requires an EditorImageViewModel DataContext.");

        Initialize(viewModel);

        _isInitialized = true;
    }

    private void Initialize(EditorImageViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewModel.ImagePath);

        _viewModel = viewModel;

        BitmapImage bitmap = LoadBitmap(new Uri(viewModel.ImagePath, UriKind.Absolute));

        InitializeViewModelSize(bitmap);

        Width = viewModel.Width;
        Height = viewModel.Height;

        _rotationTransform = new RotateTransform(viewModel.Rotation);
        RenderTransform = _rotationTransform;

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

        _editorCanvas = GetEditorCanvas();

        if (_editorCanvas is not null)
            _editorCanvas.PreviewMouseLeftButtonDown += OnEditorPreviewMouseLeftButtonDown;

        UpdateChrome();
    }

    private void InitializeViewModelSize(BitmapSource bitmap)
    {
        if (_viewModel.Width > 0 && _viewModel.Height > 0)
            return;

        Size size = CalculateInitialSize(bitmap.PixelWidth, bitmap.PixelHeight, DefaultSize);

        _viewModel.Width = size.Width;
        _viewModel.Height = size.Height;
    }

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

    private void RegisterInteractionEvents()
    {
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        MouseMove += OnMouseMove;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        KeyDown += OnKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (_editorCanvas is not null)
            _editorCanvas.PreviewMouseLeftButtonDown -= OnEditorPreviewMouseLeftButtonDown;

        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    #endregion

    #region Visual Creation

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

    private static Size CalculateInitialSize(int pixelWidth, int pixelHeight, double maximumDimension)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return new Size(maximumDimension, maximumDimension);

        double scale = Math.Min(maximumDimension / pixelWidth, maximumDimension / pixelHeight);
        scale = Math.Min(scale, 1);

        return new Size(
            Math.Max(MinimumSize, pixelWidth * scale),
            Math.Max(MinimumSize, pixelHeight * scale));
    }

    #endregion

    #region ViewModel Events

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorImageViewModel.IsSelected))
            UpdateChrome();
    }

    #endregion

    #region Parent Interaction

    private void OnEditorPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsSelected)
            return;

        if (e.OriginalSource is not DependencyObject source || !IsDescendantOf(source, this))
            Deselect();
    }

    private static bool IsDescendantOf(DependencyObject element, DependencyObject ancestor)
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

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _isHovered = true;

        UpdateCursor(e);
        UpdateChrome();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (IsMouseCaptured)
            return;

        _isHovered = false;
        Cursor = Cursors.Arrow;

        UpdateChrome();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Select();

        ResizeCorner? corner = GetResizeCornerAtMouse(e);

        if (corner is not null)
            BeginResize(corner.Value, e);
        else if (IsMouseOverRotationHandle(e))
            BeginRotation(e);
        else
            BeginDrag(e);

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_interactionMode == InteractionMode.None)
            return;

        _interactionMode = InteractionMode.None;

        if (IsMouseCaptured)
            ReleaseMouseCapture();

        UpdateChrome();

        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
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

    private void Select()
    {
        _viewModel.SelectAction?.Invoke(_viewModel);

        Focus();
        Keyboard.Focus(this);

        UpdateChrome();
    }

    private void Deselect()
    {
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        _interactionMode = InteractionMode.None;
        _viewModel.IsSelected = false;

        UpdateChrome();
    }

    #endregion

    #region Dragging

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

        CaptureMouse();
    }

    private void Drag(MouseEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        Point mouse = e.GetPosition(canvas);
        Vector movement = mouse - _dragStartMouse;

        double maxLeft = Math.Max(0, canvas.ActualWidth - Width);
        double maxTop = Math.Max(0, canvas.ActualHeight - Height);

        double left = Math.Clamp(_dragStartLeft + movement.X, 0, maxLeft);
        double top = Math.Clamp(_dragStartTop + movement.Y, 0, maxTop);

        SetPosition(left, top);
    }

    #endregion

    #region Resizing

    private void BeginResize(ResizeCorner corner, MouseButtonEventArgs e)
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

    private void Resize(MouseEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        Point currentMouse = e.GetPosition(canvas);
        Vector screenDelta = currentMouse - _resizeStartMouse;
        Vector delta = RotateVector(screenDelta, -_rotationTransform.Angle);

        bool fromCentre = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool preserveAspectRatio = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        bool leftCorner = _activeResizeCorner is ResizeCorner.TopLeft or ResizeCorner.BottomLeft;
        bool topCorner = _activeResizeCorner is ResizeCorner.TopLeft or ResizeCorner.TopRight;

        double widthDelta = delta.X * (leftCorner ? -1 : 1);
        double heightDelta = delta.Y * (topCorner ? -1 : 1);

        if (fromCentre)
        {
            widthDelta *= 2;
            heightDelta *= 2;
        }

        double newWidth = Math.Max(MinimumSize, _resizeStartWidth + widthDelta);
        double newHeight = Math.Max(MinimumSize, _resizeStartHeight + heightDelta);

        if (preserveAspectRatio)
            ApplyAspectRatio(ref newWidth, ref newHeight);

        ApplyResize(newWidth, newHeight, leftCorner, topCorner, fromCentre, canvas);

        UpdateChrome();
    }

    private void ApplyAspectRatio(ref double width, ref double height)
    {
        double scaleX = width / _resizeStartWidth;
        double scaleY = height / _resizeStartHeight;
        double scale = Math.Abs(scaleX - 1) > Math.Abs(scaleY - 1) ? scaleX : scaleY;

        double minimumScale = Math.Max(
            MinimumSize / _resizeStartWidth,
            MinimumSize / _resizeStartHeight);

        scale = Math.Max(minimumScale, scale);

        width = _resizeStartWidth * scale;
        height = _resizeStartHeight * scale;
    }

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
            double centreX = _resizeStartLeft + (_resizeStartWidth / 2);
            double centreY = _resizeStartTop + (_resizeStartHeight / 2);

            double maxWidth = Math.Max(MinimumSize, 2 * Math.Min(centreX, parent.ActualWidth - centreX));
            double maxHeight = Math.Max(MinimumSize, 2 * Math.Min(centreY, parent.ActualHeight - centreY));

            newWidth = Math.Clamp(newWidth, MinimumSize, maxWidth);
            newHeight = Math.Clamp(newHeight, MinimumSize, maxHeight);

            newLeft = centreX - (newWidth / 2);
            newTop = centreY - (newHeight / 2);
        }
        else
        {
            double startRight = _resizeStartLeft + _resizeStartWidth;
            double startBottom = _resizeStartTop + _resizeStartHeight;

            if (leftCorner)
            {
                newWidth = Math.Min(newWidth, startRight);
                newLeft = startRight - newWidth;
            }
            else
            {
                newLeft = _resizeStartLeft;
                newWidth = Math.Min(newWidth, parent.ActualWidth - newLeft);
            }

            if (topCorner)
            {
                newHeight = Math.Min(newHeight, startBottom);
                newTop = startBottom - newHeight;
            }
            else
            {
                newTop = _resizeStartTop;
                newHeight = Math.Min(newHeight, parent.ActualHeight - newTop);
            }
        }

        Width = Math.Max(MinimumSize, newWidth);
        Height = Math.Max(MinimumSize, newHeight);

        SetPosition(Math.Max(0, newLeft), Math.Max(0, newTop));

        SyncSizeToViewModel();
    }

    #endregion

    #region Rotation

    private void BeginRotation(MouseButtonEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        _interactionMode = InteractionMode.Rotating;

        _rotationCentre = new Point(
            _viewModel.X + (Width / 2),
            _viewModel.Y + (Height / 2));

        Point mouse = e.GetPosition(canvas);

        _rotationStartMouseAngle = CalculateAngle(_rotationCentre, mouse);
        _rotationStartAngle = _rotationTransform.Angle;

        Cursor = Cursors.Hand;

        CaptureMouse();
    }

    private void Rotate(MouseEventArgs e)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        Point mouse = e.GetPosition(canvas);

        double currentMouseAngle = CalculateAngle(_rotationCentre, mouse);
        double angle = _rotationStartAngle + currentMouseAngle - _rotationStartMouseAngle;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            angle = Math.Round(angle / 15) * 15;

        angle = NormalizeAngle(angle);

        _rotationTransform.Angle = angle;
        _viewModel.Rotation = angle;
    }

    private static double CalculateAngle(Point centre, Point point)
    {
        double radians = Math.Atan2(point.Y - centre.Y, point.X - centre.X);

        return radians * 180 / Math.PI;
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360;

        if (angle < 0)
            angle += 360;

        return angle;
    }

    #endregion

    #region Keyboard

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        double nudge = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;

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

    private void Nudge(double x, double y)
    {
        Canvas? canvas = GetEditorCanvas();

        if (canvas is null)
            return;

        double left = Math.Clamp(_viewModel.X + x, 0, Math.Max(0, canvas.ActualWidth - Width));
        double top = Math.Clamp(_viewModel.Y + y, 0, Math.Max(0, canvas.ActualHeight - Height));

        SetPosition(left, top);
    }

    private void RequestDelete()
    {
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        _viewModel.DeleteAction?.Invoke(_viewModel);
    }

    #endregion

    #region Editor Chrome

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size result = base.ArrangeOverride(arrangeSize);

        if (_isInitialized)
            UpdateChrome();

        return result;
    }

    private void UpdateChrome()
    {
        if (!_isInitialized)
            return;

        double width = GetCurrentWidth();
        double height = GetCurrentHeight();

        UpdateImageAndBorder(width, height);
        PositionResizeHandles(width, height);
        PositionRotationHandle(width);

        if (_isEditorChromeSuppressed)
        {
            SetChromeVisibility(Visibility.Collapsed);
            return;
        }

        UpdateSelectionBorder();

        Visibility handleVisibility = _viewModel.IsSelected
            ? Visibility.Visible
            : Visibility.Collapsed;

        SetHandleVisibility(handleVisibility);
    }

    private void UpdateImageAndBorder(double width, double height)
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

    private void PositionResizeHandles(double width, double height)
    {
        double halfHandle = HandleSize / 2;

        PositionHandle(_topLeftHandle, -halfHandle, -halfHandle);
        PositionHandle(_topRightHandle, width - halfHandle, -halfHandle);
        PositionHandle(_bottomLeftHandle, -halfHandle, height - halfHandle);
        PositionHandle(_bottomRightHandle, width - halfHandle, height - halfHandle);
    }

    private void PositionRotationHandle(double width)
    {
        _rotationLine.X1 = width / 2;
        _rotationLine.Y1 = 0;
        _rotationLine.X2 = width / 2;
        _rotationLine.Y2 = -RotationHandleOffset + (RotationHandleSize / 2);

        SetLeft(_rotationHandle, (width / 2) - (RotationHandleSize / 2));
        SetTop(_rotationHandle, -RotationHandleOffset);
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

        _selectionBorder.Visibility = Visibility.Collapsed;
    }

    private void SetChromeVisibility(Visibility visibility)
    {
        _selectionBorder.Visibility = visibility;

        SetHandleVisibility(visibility);
    }

    private void SetHandleVisibility(Visibility visibility)
    {
        _topLeftHandle.Visibility = visibility;
        _topRightHandle.Visibility = visibility;
        _bottomLeftHandle.Visibility = visibility;
        _bottomRightHandle.Visibility = visibility;
        _rotationLine.Visibility = visibility;
        _rotationHandle.Visibility = visibility;
    }

    private static void PositionHandle(FrameworkElement handle, double left, double top)
    {
        SetLeft(handle, left);
        SetTop(handle, top);
    }

    #endregion

    #region Hit Testing and Cursor

    private ResizeCorner? GetResizeCornerAtMouse(MouseEventArgs e)
    {
        if (!_viewModel.IsSelected)
            return null;

        Point position = e.GetPosition(this);

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

    private bool IsMouseOverRotationHandle(MouseEventArgs e)
    {
        return _viewModel.IsSelected && IsInsideHandle(e.GetPosition(this), _rotationHandle);
    }

    private static bool IsInsideHandle(Point point, FrameworkElement handle)
    {
        double left = GetLeft(handle);
        double top = GetTop(handle);

        return point.X >= left - HandleHitPadding &&
               point.X <= left + handle.Width + HandleHitPadding &&
               point.Y >= top - HandleHitPadding &&
               point.Y <= top + handle.Height + HandleHitPadding;
    }

    private void UpdateCursor(MouseEventArgs e)
    {
        ResizeCorner? corner = GetResizeCornerAtMouse(e);

        if (corner is not null)
        {
            UpdateCursorForCorner(corner.Value);
            return;
        }

        Cursor = IsMouseOverRotationHandle(e) ? Cursors.Hand : Cursors.SizeAll;
    }

    private void UpdateCursorForCorner(ResizeCorner corner)
    {
        Cursor = corner switch
        {
            ResizeCorner.TopLeft or ResizeCorner.BottomRight => Cursors.SizeNWSE,
            ResizeCorner.TopRight or ResizeCorner.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Arrow
        };
    }

    #endregion

    #region ViewModel Synchronization

    private void SetPosition(double left, double top)
    {
        _viewModel.X = left;
        _viewModel.Y = top;
    }

    private void SyncSizeToViewModel()
    {
        _viewModel.Width = Width;
        _viewModel.Height = Height;
    }

    #endregion

    #region Helpers

    private Canvas? GetEditorCanvas()
    {
        DependencyObject? current = VisualTreeHelper.GetParent(this);

        while (current is not null)
        {
            if (current is Canvas canvas)
                return canvas;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private double GetCurrentWidth() => double.IsNaN(Width) ? ActualWidth : Width;

    private double GetCurrentHeight() => double.IsNaN(Height) ? ActualHeight : Height;

    private static Vector RotateVector(Vector vector, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        return new Vector(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos));
    }

    #endregion

    #region Internal Types

    private enum InteractionMode
    {
        None,
        Dragging,
        Resizing,
        Rotating
    }

    private enum ResizeCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    #endregion
}