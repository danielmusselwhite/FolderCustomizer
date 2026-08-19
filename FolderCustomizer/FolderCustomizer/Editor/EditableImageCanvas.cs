using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FolderCustomizer.Editor;

public sealed class EditableImageCanvas : Canvas
{
    private const double DefaultSize = 180;
    private const double MinimumSize = 24;

    private const double HandleSize = 12;
    private const double HandleHitPadding = 6;

    private const double RotationHandleSize = 14;
    private const double RotationHandleOffset = 30;

    private static readonly Brush AccentBrush =
        new SolidColorBrush(Color.FromRgb(0, 103, 192));

    private static readonly Brush HoverBrush =
        new SolidColorBrush(Color.FromArgb(160, 0, 103, 192));

    private readonly Image _image;
    private readonly Border _selectionBorder;

    private readonly Rectangle _topLeftHandle;
    private readonly Rectangle _topRightHandle;
    private readonly Rectangle _bottomLeftHandle;
    private readonly Rectangle _bottomRightHandle;

    private readonly Line _rotationLine;
    private readonly Ellipse _rotationHandle;

    private readonly RotateTransform _rotationTransform;

    private bool _isSelected;
    private bool _isHovered;

    private InteractionMode _interactionMode;
    private ResizeCorner _activeResizeCorner;

    // Drag state
    private Point _dragStartMouse;
    private double _dragStartLeft;
    private double _dragStartTop;

    // Resize state
    private Point _resizeStartMouse;

    private double _resizeStartLeft;
    private double _resizeStartTop;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    // Rotation state
    private Point _rotationCentre;
    private double _rotationStartAngle;
    private double _rotationStartMouseAngle;

    public EditableImageCanvas(Uri imagePath)
    {
        ArgumentNullException.ThrowIfNull(imagePath);

        Focusable = true;
        ClipToBounds = false;
        Background = Brushes.Transparent;

        RenderTransformOrigin = new Point(0.5, 0.5);

        _rotationTransform = new RotateTransform();
        RenderTransform = _rotationTransform;

        BitmapImage bitmap = LoadBitmap(imagePath);

        Size initialSize = CalculateInitialSize(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            DefaultSize);

        Width = initialSize.Width;
        Height = initialSize.Height;

        _image = CreateImage(bitmap);

        _selectionBorder = CreateSelectionBorder();

        _topLeftHandle = CreateResizeHandle(Cursors.SizeNWSE);
        _topRightHandle = CreateResizeHandle(Cursors.SizeNESW);
        _bottomLeftHandle = CreateResizeHandle(Cursors.SizeNESW);
        _bottomRightHandle = CreateResizeHandle(Cursors.SizeNWSE);

        _rotationLine = CreateRotationLine();
        _rotationHandle = CreateRotationHandle();

        Children.Add(_image);
        Children.Add(_selectionBorder);
        Children.Add(_rotationLine);

        Children.Add(_topLeftHandle);
        Children.Add(_topRightHandle);
        Children.Add(_bottomLeftHandle);
        Children.Add(_bottomRightHandle);

        Children.Add(_rotationHandle);

        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;

        KeyDown += OnKeyDown;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        UpdateChrome();
    }
    // =====================================================================
    // Public state
    // =====================================================================

    public double Rotation => _rotationTransform.Angle;

    // =====================================================================
    // Creation
    // =====================================================================

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

    private static Size CalculateInitialSize(
    int pixelWidth,
    int pixelHeight,
    double maximumDimension)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return new Size(
                maximumDimension,
                maximumDimension);
        }

        double scale = Math.Min(
            maximumDimension / pixelWidth,
            maximumDimension / pixelHeight);

        // Don't enlarge small images on initial insert.
        scale = Math.Min(scale, 1.0);

        return new Size(
            Math.Max(MinimumSize, pixelWidth * scale),
            Math.Max(MinimumSize, pixelHeight * scale));
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

    private static Rectangle CreateResizeHandle(
        Cursor cursor)
    {
        return new Rectangle
        {
            Width = HandleSize,
            Height = HandleSize,

            RadiusX = 3,
            RadiusY = 3,

            Fill = AccentBrush,

            Stroke = Brushes.White,
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

    // =====================================================================
    // Parent canvas
    // =====================================================================

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (Parent is UIElement parent)
        {
            parent.PreviewMouseLeftButtonDown +=
                OnParentPreviewMouseLeftButtonDown;
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (Parent is UIElement parent)
        {
            parent.PreviewMouseLeftButtonDown -=
                OnParentPreviewMouseLeftButtonDown;
        }
    }

    private void OnParentPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isSelected)
            return;

        if (e.OriginalSource is not DependencyObject source)
        {
            Deselect();
            return;
        }

        // Keep selection when clicking anywhere within this element.
        if (IsDescendantOf(source, this))
            return;

        Deselect();
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

    // =====================================================================
    // Hover
    // =====================================================================

    private void OnMouseEnter(
        object sender,
        MouseEventArgs e)
    {
        _isHovered = true;

        Cursor = Cursors.SizeAll;

        UpdateChrome();
    }

    private void OnMouseLeave(
        object sender,
        MouseEventArgs e)
    {
        if (IsMouseCaptured)
            return;

        _isHovered = false;

        Cursor = Cursors.Arrow;

        UpdateChrome();
    }

    // =====================================================================
    // Mouse input
    // =====================================================================

    private void OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        Select();

        ResizeCorner? resizeCorner =
            GetResizeCornerAtMouse(e);

        if (resizeCorner is not null)
        {
            BeginResize(
                resizeCorner.Value,
                e);

            e.Handled = true;

            return;
        }

        if (IsMouseOverRotationHandle(e))
        {
            BeginRotation(e);

            e.Handled = true;

            return;
        }

        BeginDrag(e);

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_interactionMode == InteractionMode.None)
            return;

        _interactionMode =
            InteractionMode.None;

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

    // =====================================================================
    // Selection
    // =====================================================================

    private void Select()
    {
        // Deselect other overlay images first.
        if (Parent is Panel parent)
        {
            foreach (UIElement child in parent.Children)
            {
                if (child is EditableImageCanvas other &&
                    !ReferenceEquals(other, this))
                {
                    other.Deselect();
                }
            }
        }

        _isSelected = true;

        Focus();
        Keyboard.Focus(this);

        UpdateChrome();
    }

    private void Deselect()
    {
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        _interactionMode =
            InteractionMode.None;

        _isSelected = false;

        UpdateChrome();
    }

    // =====================================================================
    // Drag
    // =====================================================================

    private void BeginDrag(
        MouseButtonEventArgs e)
    {
        if (Parent is not Canvas parent)
            return;

        _interactionMode =
            InteractionMode.Dragging;

        _dragStartMouse =
            e.GetPosition(parent);

        _dragStartLeft =
            GetCanvasLeft();

        _dragStartTop =
            GetCanvasTop();

        Cursor = Cursors.SizeAll;

        CaptureMouse();
    }

    private void Drag(
        MouseEventArgs e)
    {
        if (Parent is not Canvas parent)
            return;

        Point mouse =
            e.GetPosition(parent);

        Vector movement =
            mouse -
            _dragStartMouse;

        double left =
            _dragStartLeft +
            movement.X;

        double top =
            _dragStartTop +
            movement.Y;

        double maxLeft =
            Math.Max(
                0,
                parent.ActualWidth - Width);

        double maxTop =
            Math.Max(
                0,
                parent.ActualHeight - Height);

        left = Math.Clamp(
            left,
            0,
            maxLeft);

        top = Math.Clamp(
            top,
            0,
            maxTop);

        SetLeft(this, left);
        SetTop(this, top);
    }

    // =====================================================================
    // Resize
    // =====================================================================

    private void BeginResize(
        ResizeCorner corner,
        MouseButtonEventArgs e)
    {
        if (Parent is not Canvas parent)
            return;

        _interactionMode =
            InteractionMode.Resizing;

        _activeResizeCorner =
            corner;

        _resizeStartMouse =
            e.GetPosition(parent);

        _resizeStartLeft =
            GetCanvasLeft();

        _resizeStartTop =
            GetCanvasTop();

        _resizeStartWidth =
            Width;

        _resizeStartHeight =
            Height;

        CaptureMouse();

        UpdateCursorForCorner(corner);
    }

    private void Resize(
        MouseEventArgs e)
    {
        if (Parent is not Canvas parent)
            return;

        Point currentMouse =
            e.GetPosition(parent);

        Vector screenDelta =
            currentMouse -
            _resizeStartMouse;

        // Convert the mouse movement back into the image's local axes
        // so resizing still behaves naturally after rotation.
        Vector delta =
            RotateVector(
                screenDelta,
                -_rotationTransform.Angle);

        bool fromCentre =
            Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control);

        bool preserveAspectRatio =
            Keyboard.Modifiers.HasFlag(
                ModifierKeys.Shift);

        bool leftCorner =
            _activeResizeCorner is
                ResizeCorner.TopLeft or
                ResizeCorner.BottomLeft;

        bool topCorner =
            _activeResizeCorner is
                ResizeCorner.TopLeft or
                ResizeCorner.TopRight;

        double horizontalDirection =
            leftCorner
                ? -1
                : 1;

        double verticalDirection =
            topCorner
                ? -1
                : 1;

        double widthDelta =
            delta.X *
            horizontalDirection;

        double heightDelta =
            delta.Y *
            verticalDirection;

        if (fromCentre)
        {
            widthDelta *= 2;
            heightDelta *= 2;
        }

        double newWidth =
            Math.Max(
                MinimumSize,
                _resizeStartWidth +
                widthDelta);

        double newHeight =
            Math.Max(
                MinimumSize,
                _resizeStartHeight +
                heightDelta);

        if (preserveAspectRatio)
        {
            double scaleX =
                newWidth /
                _resizeStartWidth;

            double scaleY =
                newHeight /
                _resizeStartHeight;

            double scale =
                Math.Abs(scaleX - 1) >
                Math.Abs(scaleY - 1)
                    ? scaleX
                    : scaleY;

            scale = Math.Max(
                MinimumSize /
                Math.Min(
                    _resizeStartWidth,
                    _resizeStartHeight),
                scale);

            newWidth =
                _resizeStartWidth *
                scale;

            newHeight =
                _resizeStartHeight *
                scale;
        }

        ApplyResize(
            newWidth,
            newHeight,
            leftCorner,
            topCorner,
            fromCentre,
            parent);

        UpdateChrome();
    }

    private void ApplyResize(
        double newWidth,
        double newHeight,
        bool leftCorner,
        bool topCorner,
        bool fromCentre,
        Canvas parent)
    {
        double startRight =
            _resizeStartLeft +
            _resizeStartWidth;

        double startBottom =
            _resizeStartTop +
            _resizeStartHeight;

        double centreX =
            _resizeStartLeft +
            (_resizeStartWidth / 2);

        double centreY =
            _resizeStartTop +
            (_resizeStartHeight / 2);

        double newLeft;
        double newTop;

        if (fromCentre)
        {
            double maximumWidth =
                2 *
                Math.Min(
                    centreX,
                    parent.ActualWidth - centreX);

            double maximumHeight =
                2 *
                Math.Min(
                    centreY,
                    parent.ActualHeight - centreY);

            newWidth =
                Math.Clamp(
                    newWidth,
                    MinimumSize,
                    Math.Max(
                        MinimumSize,
                        maximumWidth));

            newHeight =
                Math.Clamp(
                    newHeight,
                    MinimumSize,
                    Math.Max(
                        MinimumSize,
                        maximumHeight));

            newLeft =
                centreX -
                (newWidth / 2);

            newTop =
                centreY -
                (newHeight / 2);
        }
        else
        {
            if (leftCorner)
            {
                newWidth =
                    Math.Min(
                        newWidth,
                        startRight);

                newLeft =
                    startRight -
                    newWidth;
            }
            else
            {
                newLeft =
                    _resizeStartLeft;

                newWidth =
                    Math.Min(
                        newWidth,
                        parent.ActualWidth -
                        newLeft);
            }

            if (topCorner)
            {
                newHeight =
                    Math.Min(
                        newHeight,
                        startBottom);

                newTop =
                    startBottom -
                    newHeight;
            }
            else
            {
                newTop =
                    _resizeStartTop;

                newHeight =
                    Math.Min(
                        newHeight,
                        parent.ActualHeight -
                        newTop);
            }
        }

        Width =
            Math.Max(
                MinimumSize,
                newWidth);

        Height =
            Math.Max(
                MinimumSize,
                newHeight);

        SetLeft(
            this,
            Math.Max(0, newLeft));

        SetTop(
            this,
            Math.Max(0, newTop));
    }

    // =====================================================================
    // Rotation
    // =====================================================================

    private void BeginRotation(
        MouseButtonEventArgs e)
    {
        if (Parent is not Canvas parent)
            return;

        _interactionMode =
            InteractionMode.Rotating;

        double left =
            GetCanvasLeft();

        double top =
            GetCanvasTop();

        _rotationCentre =
            new Point(
                left + (Width / 2),
                top + (Height / 2));

        Point mouse =
            e.GetPosition(parent);

        _rotationStartMouseAngle =
            CalculateAngle(
                _rotationCentre,
                mouse);

        _rotationStartAngle =
            _rotationTransform.Angle;

        Cursor = Cursors.Hand;

        CaptureMouse();
    }

    private void Rotate(
        MouseEventArgs e)
    {
        if (Parent is not Canvas parent)
            return;

        Point mouse =
            e.GetPosition(parent);

        double currentMouseAngle =
            CalculateAngle(
                _rotationCentre,
                mouse);

        double delta =
            currentMouseAngle -
            _rotationStartMouseAngle;

        double angle =
            _rotationStartAngle +
            delta;

        // Shift while rotating snaps to 15 degree increments.
        if (Keyboard.Modifiers.HasFlag(
                ModifierKeys.Shift))
        {
            angle =
                Math.Round(angle / 15) *
                15;
        }

        _rotationTransform.Angle =
            NormalizeAngle(angle);
    }

    private static double CalculateAngle(
        Point centre,
        Point point)
    {
        double radians =
            Math.Atan2(
                point.Y - centre.Y,
                point.X - centre.X);

        return radians *
               180 /
               Math.PI;
    }

    private static double NormalizeAngle(
        double angle)
    {
        angle %= 360;

        if (angle < 0)
            angle += 360;

        return angle;
    }

    // =====================================================================
    // Keyboard
    // =====================================================================

    private void OnKeyDown(
        object sender,
        KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Delete:
            case Key.Back:

                RemoveFromEditor();

                e.Handled = true;

                break;

            case Key.Left:

                Nudge(-GetNudgeAmount(), 0);

                e.Handled = true;

                break;

            case Key.Right:

                Nudge(GetNudgeAmount(), 0);

                e.Handled = true;

                break;

            case Key.Up:

                Nudge(0, -GetNudgeAmount());

                e.Handled = true;

                break;

            case Key.Down:

                Nudge(0, GetNudgeAmount());

                e.Handled = true;

                break;
        }
    }

    private static double GetNudgeAmount()
    {
        return Keyboard.Modifiers.HasFlag(
            ModifierKeys.Shift)
                ? 10
                : 1;
    }

    private void Nudge(
        double x,
        double y)
    {
        if (Parent is not Canvas parent)
            return;

        double left =
            Math.Clamp(
                GetCanvasLeft() + x,
                0,
                Math.Max(
                    0,
                    parent.ActualWidth - Width));

        double top =
            Math.Clamp(
                GetCanvasTop() + y,
                0,
                Math.Max(
                    0,
                    parent.ActualHeight - Height));

        SetLeft(this, left);
        SetTop(this, top);
    }

    private void RemoveFromEditor()
    {
        ReleaseMouseCapture();

        if (Parent is Panel parent)
            parent.Children.Remove(this);
    }

    // =====================================================================
    // Chrome
    // =====================================================================

    protected override Size ArrangeOverride(
        Size arrangeSize)
    {
        Size result =
            base.ArrangeOverride(arrangeSize);

        UpdateChrome();

        return result;
    }

    private void UpdateChrome()
    {
        double width =
            GetCurrentWidth();

        double height =
            GetCurrentHeight();

        _image.Width = width;
        _image.Height = height;

        SetLeft(_image, 0);
        SetTop(_image, 0);

        // Selection / hover border.
        _selectionBorder.Width = width;
        _selectionBorder.Height = height;

        SetLeft(_selectionBorder, 0);
        SetTop(_selectionBorder, 0);

        if (_isSelected)
        {
            _selectionBorder.BorderBrush =
                AccentBrush;

            _selectionBorder.Opacity = 1;

            _selectionBorder.Visibility =
                Visibility.Visible;
        }
        else if (_isHovered)
        {
            _selectionBorder.BorderBrush =
                HoverBrush;

            _selectionBorder.Opacity = 0.8;

            _selectionBorder.Visibility =
                Visibility.Visible;
        }
        else
        {
            _selectionBorder.Visibility =
                Visibility.Collapsed;
        }

        // Corner handles.
        PositionHandle(
            _topLeftHandle,
            -(HandleSize / 2),
            -(HandleSize / 2));

        PositionHandle(
            _topRightHandle,
            width - (HandleSize / 2),
            -(HandleSize / 2));

        PositionHandle(
            _bottomLeftHandle,
            -(HandleSize / 2),
            height - (HandleSize / 2));

        PositionHandle(
            _bottomRightHandle,
            width - (HandleSize / 2),
            height - (HandleSize / 2));

        // Rotation connection line.
        _rotationLine.X1 =
            width / 2;

        _rotationLine.Y1 = 0;

        _rotationLine.X2 =
            width / 2;

        _rotationLine.Y2 =
            -RotationHandleOffset +
            (RotationHandleSize / 2);

        // Rotation handle.
        SetLeft(
            _rotationHandle,
            (width / 2) -
            (RotationHandleSize / 2));

        SetTop(
            _rotationHandle,
            -RotationHandleOffset);

        Visibility handleVisibility =
            _isSelected
                ? Visibility.Visible
                : Visibility.Collapsed;

        _topLeftHandle.Visibility =
            handleVisibility;

        _topRightHandle.Visibility =
            handleVisibility;

        _bottomLeftHandle.Visibility =
            handleVisibility;

        _bottomRightHandle.Visibility =
            handleVisibility;

        _rotationLine.Visibility =
            handleVisibility;

        _rotationHandle.Visibility =
            handleVisibility;
    }

    private static void PositionHandle(
        FrameworkElement handle,
        double left,
        double top)
    {
        SetLeft(handle, left);
        SetTop(handle, top);
    }

    // =====================================================================
    // Hit testing
    // =====================================================================

    private ResizeCorner? GetResizeCornerAtMouse(
        MouseEventArgs e)
    {
        if (!_isSelected)
            return null;

        Point position =
            e.GetPosition(this);

        if (IsInsideHandle(
                position,
                _topLeftHandle))
        {
            return ResizeCorner.TopLeft;
        }

        if (IsInsideHandle(
                position,
                _topRightHandle))
        {
            return ResizeCorner.TopRight;
        }

        if (IsInsideHandle(
                position,
                _bottomLeftHandle))
        {
            return ResizeCorner.BottomLeft;
        }

        if (IsInsideHandle(
                position,
                _bottomRightHandle))
        {
            return ResizeCorner.BottomRight;
        }

        return null;
    }

    private bool IsMouseOverRotationHandle(
        MouseEventArgs e)
    {
        if (!_isSelected)
            return false;

        Point position =
            e.GetPosition(this);

        return IsInsideHandle(
            position,
            _rotationHandle);
    }

    private static bool IsInsideHandle(
        Point point,
        FrameworkElement handle)
    {
        double left =
            GetLeft(handle);

        double top =
            GetTop(handle);

        return
            point.X >=
                left - HandleHitPadding &&
            point.X <=
                left +
                handle.Width +
                HandleHitPadding &&
            point.Y >=
                top -
                HandleHitPadding &&
            point.Y <=
                top +
                handle.Height +
                HandleHitPadding;
    }

    // =====================================================================
    // Cursor
    // =====================================================================

    private void UpdateCursor(
        MouseEventArgs e)
    {
        if (!_isSelected)
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        ResizeCorner? corner =
            GetResizeCornerAtMouse(e);

        if (corner is not null)
        {
            UpdateCursorForCorner(
                corner.Value);

            return;
        }

        if (IsMouseOverRotationHandle(e))
        {
            Cursor = Cursors.Hand;
            return;
        }

        Cursor = Cursors.SizeAll;
    }

    private void UpdateCursorForCorner(
        ResizeCorner corner)
    {
        Cursor =
            corner switch
            {
                ResizeCorner.TopLeft
                    => Cursors.SizeNWSE,

                ResizeCorner.BottomRight
                    => Cursors.SizeNWSE,

                ResizeCorner.TopRight
                    => Cursors.SizeNESW,

                ResizeCorner.BottomLeft
                    => Cursors.SizeNESW,

                _
                    => Cursors.Arrow
            };
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private double GetCanvasLeft()
    {
        double value =
            GetLeft(this);

        return double.IsNaN(value)
            ? 0
            : value;
    }

    private double GetCanvasTop()
    {
        double value =
            GetTop(this);

        return double.IsNaN(value)
            ? 0
            : value;
    }

    private double GetCurrentWidth()
    {
        return double.IsNaN(Width)
            ? ActualWidth
            : Width;
    }

    private double GetCurrentHeight()
    {
        return double.IsNaN(Height)
            ? ActualHeight
            : Height;
    }

    private static Vector RotateVector(
        Vector vector,
        double degrees)
    {
        double radians =
            degrees *
            Math.PI /
            180;

        double cos =
            Math.Cos(radians);

        double sin =
            Math.Sin(radians);

        return new Vector(
            (vector.X * cos) -
            (vector.Y * sin),

            (vector.X * sin) +
            (vector.Y * cos));
    }

    // =====================================================================
    // Internal state
    // =====================================================================

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

    public void HideEditorChrome()
    {
        _selectionBorder.Visibility = Visibility.Collapsed;

        _topLeftHandle.Visibility = Visibility.Collapsed;
        _topRightHandle.Visibility = Visibility.Collapsed;
        _bottomLeftHandle.Visibility = Visibility.Collapsed;
        _bottomRightHandle.Visibility = Visibility.Collapsed;

        _rotationLine.Visibility = Visibility.Collapsed;
        _rotationHandle.Visibility = Visibility.Collapsed;
    }

    public void RestoreEditorChrome()
    {
        UpdateChrome();
    }
}