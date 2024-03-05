using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Windows.Automation.Peers;

namespace FolderCustomizer.Editor
{
    public class EditableImageCanvas : Canvas
    {
        protected Rectangle[] rectangles = new Rectangle[5];
        private Point start;
        private Point origin;
        private bool isMouseOverCenterRectangle = false;
        private bool isMouseOverBottomRightRectangle = false;

        public EditableImageCanvas(Uri imagePath)
        {
            this.Height = 180;
            this.Width = 180;


            // Add the image as a child
            Image image = new Image();
            image.Source = new BitmapImage(imagePath);
            image.Width = 180;
            image.Height = 180;
            image.Stretch = Stretch.Fill;
            this.Children.Add(image);

            // Add event handlers for mouse events
            this.MouseLeftButtonDown += new MouseButtonEventHandler(EditableImageCanvas_MouseLeftButtonDown);
            this.MouseLeftButtonUp += new MouseButtonEventHandler(EditableImageCanvas_MouseLeftButtonUp);
            this.MouseMove += new MouseEventHandler(EditableImageCanvas_MouseMove);
            this.MouseLeave += new MouseEventHandler(EditableImageCanvas_MouseMove);
            this.MouseEnter += new MouseEventHandler(EditableImageCanvas_MouseEnter);
            this.MouseLeave += new MouseEventHandler(EditableImageCanvas_MouseLeave);


            // Add a red rectangle in each corner of the canvas
            for (int i = 0; i < rectangles.Length; i++)
            {
                rectangles[i] = new Rectangle();
                rectangles[i].Width = 15;
                rectangles[i].Height = 15;
                rectangles[i].Fill = Brushes.Red;
                rectangles[i].Visibility = Visibility.Hidden;

                // set the coordinate for each rectangle, in order left-top, right-top, left-bottom, right-bottom

                switch (i)
                {
                    case 0:
                        rectangles[i].Name = "leftTop";
                        Canvas.SetLeft(rectangles[i], 0);
                        Canvas.SetTop(rectangles[i], 0);
                        break;
                    case 1:
                        rectangles[i].Name = "rightTop";
                        Canvas.SetLeft(rectangles[i], this.Width - rectangles[i].Width);
                        Canvas.SetTop(rectangles[i], 0);
                        break;
                    case 2:
                        rectangles[i].Name = "leftBottom";
                        Canvas.SetLeft(rectangles[i], 0);
                        Canvas.SetTop(rectangles[i], this.Height - rectangles[i].Height);
                        break;
                    case 3:
                        rectangles[i].Name = "rightBottom";
                        Canvas.SetLeft(rectangles[i], this.Width - rectangles[i].Width);
                        Canvas.SetTop(rectangles[i], this.Height - rectangles[i].Height);
                        break;
                }

                this.Children.Add(rectangles[i]);

            }

            // Add rectangle in the centre
            rectangles[4] = new Rectangle();
            rectangles[4].Width = 15;
            rectangles[4].Height = 15;
            rectangles[4].Fill = Brushes.Red;
            rectangles[4].Visibility = Visibility.Hidden;
            rectangles[4].Name = "center";
            Canvas.SetLeft(rectangles[4], this.Width / 2 - rectangles[4].Width / 2);
            Canvas.SetTop(rectangles[4], this.Height / 2 - rectangles[4].Height / 2);
            this.Children.Add(rectangles[4]);



        }

        private void EditableImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the mouse is over any of the rectangles
            isMouseOverCenterRectangle = IsMouseOverCenterRectangle();
            isMouseOverBottomRightRectangle = IsMouseOverBottomRightRectangle();


            // Capture and remember the mouse position
            this.CaptureMouse();
            start = e.GetPosition(this);
        }

        private void EditableImageCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the flag when mouse capture is released
            isMouseOverCenterRectangle = false;
            isMouseOverBottomRightRectangle = false;

            // Release the mouse capture
            this.ReleaseMouseCapture();
        }

        private void EditableImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Move the image if the mouse is captured over the centre rectangle
            if (this.IsMouseCaptured && isMouseOverCenterRectangle)
            {
                // Get the position of the mouse relative to the canvas
                Point position = e.GetPosition(this.Parent as UIElement);

                // Move the image
                this.RenderTransform = new TranslateTransform(position.X - start.X + origin.X, position.Y - start.Y + origin.Y);
            }

            // Resize the canvas and all its children if the mouse is captured over the bottom-right rectangle
            if (this.IsMouseCaptured && isMouseOverBottomRightRectangle)
            {
                // Get the position of the mouse relative to the canvas
                Point position = e.GetPosition(this);

                // Ensure minimum resize to 40x40
                position.X = Math.Max(40, position.X);
                position.Y = Math.Max(40, position.Y);

                ResizeCanvasAndChildren(position);
                ResizeRectangles();
            }
        }

        private void ResizeCanvasAndChildren(Point position)
        {
            // Resize the canvas
            this.Width = position.X;
            this.Height = position.Y;

            // Resize the image
            foreach (UIElement element in this.Children)
            {
                if (element is Image)
                {
                    (element as Image).Width = position.X;
                    (element as Image).Height = position.Y;
                    (element as Image).Stretch = Stretch.Fill;
                }
            }
        }

        private void ResizeRectangles()
        {
            // Resize the rectangles
            foreach (Rectangle rectangle in rectangles)
            {
                switch (rectangle.Name)
                {
                    case "leftTop":
                        Canvas.SetLeft(rectangle, 0);
                        Canvas.SetTop(rectangle, 0);
                        break;
                    case "rightTop":
                        Canvas.SetLeft(rectangle, this.Width - rectangle.Width);
                        Canvas.SetTop(rectangle, 0);
                        break;
                    case "leftBottom":
                        Canvas.SetLeft(rectangle, 0);
                        Canvas.SetTop(rectangle, this.Height - rectangle.Height);
                        break;
                    case "rightBottom":
                        Canvas.SetLeft(rectangle, this.Width - rectangle.Width);
                        Canvas.SetTop(rectangle, this.Height - rectangle.Height);
                        break;
                    case "center":
                        Canvas.SetLeft(rectangle, this.Width / 2 - rectangle.Width / 2);
                        Canvas.SetTop(rectangle, this.Height / 2 - rectangle.Height / 2);
                        break;
                }
            }
        }

        private void EditableImageCanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            // Show the rectangles
            foreach (Rectangle rectangle in rectangles)
            {
                if(rectangle.Name == "center" || rectangle.Name == "rightBottom")
                    rectangle.Visibility = Visibility.Visible;
            }
        }

        private void EditableImageCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            // Hide the rectangles
            foreach (Rectangle rectangle in rectangles)
            {
                rectangle.Visibility = Visibility.Hidden;
            }
        }

        private bool IsMouseOverCenterRectangle()
        {
            // Get the position of the mouse relative to the canvas
            Point position = Mouse.GetPosition(this);

            // Check if the mouse is over the center rectangle
            if (position.X >= Canvas.GetLeft(rectangles[4]) && position.X <= Canvas.GetLeft(rectangles[4]) + rectangles[4].Width &&
                position.Y >= Canvas.GetTop(rectangles[4]) && position.Y <= Canvas.GetTop(rectangles[4]) + rectangles[4].Height)
            {
                return true;
            }

            return false;
        }

        private bool IsMouseOverBottomRightRectangle()
        {
            // Get the position of the mouse relative to the canvas
            Point position = Mouse.GetPosition(this);

            // Check if the mouse is over the bottom-right rectangle
            if (position.X >= Canvas.GetLeft(rectangles[3]) && position.X <= Canvas.GetLeft(rectangles[3]) + rectangles[3].Width &&
                               position.Y >= Canvas.GetTop(rectangles[3]) && position.Y <= Canvas.GetTop(rectangles[3]) + rectangles[3].Height)
            {
                return true;
            }

            return false;
        }

    }

}
