using FolderCustomizer.Editor;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;

namespace FolderCustomizer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // hide everything besides the btn_LoadFolder
            iconEditorCanvas.Visibility = Visibility.Hidden;
            btn_addImage.Visibility = Visibility.Hidden;
            btn_saveImg.Visibility = Visibility.Hidden;
            txt_SelectedFolder.Visibility = Visibility.Hidden;
        }

        private string folderPath = "";

        private void Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select FOLDER/DIRECTORY to customize
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select a folder to customize";
            folderBrowserDialog.ShowDialog();
            updateSelectedFolderTxt(folderBrowserDialog.SelectedPath);

            updateCanvas(folderBrowserDialog.SelectedPath);

            folderPath = folderBrowserDialog.SelectedPath;

            // show everything
            iconEditorCanvas.Visibility = Visibility.Visible;
            btn_addImage.Visibility = Visibility.Visible;
            btn_saveImg.Visibility = Visibility.Visible;
            txt_SelectedFolder.Visibility = Visibility.Visible;
        }

        private void updateSelectedFolderTxt(string folderPath)
        {
            // Update the textbox with the selected folder or folder
            txt_SelectedFolder.Text = folderPath;
        }

        private void updateCanvas(string folderPath)
        {
            // Update the canvas with the selected folder or folder
            // Create a new instance of the canvas
            Canvas canvas = iconEditorCanvas;

            // Clear the canvas
            canvas.Children.Clear();

            // Create a editable image with Uri("pack://application:,,,/res/folder.png") to let user resize, rotate, and move the image
            System.Windows.Controls.Image folderIcon = new System.Windows.Controls.Image();
            folderIcon.Source = new BitmapImage(new Uri("pack://application:,,,/res/folder.png"));
            folderIcon.Width = canvas.Width;
            folderIcon.Height = canvas.Height;
            canvas.Children.Add(folderIcon);
        }

        private void Btn_AddImage_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select IMAGE to add to the canvas
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg";
            openFileDialog.ShowDialog();

            // Add an EditableImageCanvas to the iconEditorCanvas
            Canvas canvas = iconEditorCanvas;
            EditableImageCanvas imageEditable = new EditableImageCanvas(new Uri(openFileDialog.FileName));
            canvas.Children.Add(imageEditable);
        }

        private void Btn_UpdateFolder_Icon(object sender, RoutedEventArgs e)
        {
            // Get the actual size of the canvas content
            Size contentSize = VisualTreeHelper.GetDescendantBounds(iconEditorCanvas).Size;

            // Calculate the offset needed to center the content
            double offsetX = (iconEditorCanvas.ActualWidth - contentSize.Width) / 2;
            double offsetY = (iconEditorCanvas.ActualHeight - contentSize.Height) / 2;

            // Create a render target bitmap with a size large enough to contain the centered canvas content
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                (int)iconEditorCanvas.ActualWidth, (int)iconEditorCanvas.ActualHeight,
                96d, 96d, System.Windows.Media.PixelFormats.Pbgra32);

            // Render the canvas content to the bitmap with the calculated offset
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(new VisualBrush(iconEditorCanvas), null, new Rect(new System.Windows.Point(0, 0), contentSize));
            }
            renderBitmap.Render(drawingVisual);

            // Save the bitmap as a PNG file
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            string iconFilePath = System.IO.Path.Combine(folderPath, "custom_icon.png");
            using (var fileStream = new System.IO.FileStream(iconFilePath, System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }

            // Convert PNG to ICO
            string icoPath = ConvertToIcon(iconFilePath, System.IO.Path.Combine(folderPath, "custom_icon.ico"));

            UpdateFolderIcon(icoPath, folderPath);
        }

        private string ConvertToIcon(string sourceImagePath, string destinationIconPath)
        {
            using (System.IO.FileStream inputStream = new System.IO.FileStream(sourceImagePath, System.IO.FileMode.Open))
            {
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(inputStream))
                {
                    using (System.IO.FileStream outputStream = new System.IO.FileStream(destinationIconPath, System.IO.FileMode.Create))
                    {
                        // Create an icon with the same size as the bitmap
                        System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());

                        // Save the icon with the original colors and alpha channel intact
                        icon.Save(outputStream);

                        return destinationIconPath;
                    }
                }
            }
        }

        private void UpdateFolderIcon(string icoFilePath, string folderPath)
        {
            // update the .ini file to look like this:
            /*
             * [.ShellClassInfo]
             * IconResource=custom_icon.ico,0  
             */

            string iniFilePath = System.IO.Path.Combine(folderPath, "desktop.ini");
            string iconFilePath = icoFilePath;

            // make the folder a system folder
            System.IO.File.SetAttributes(folderPath, System.IO.File.GetAttributes(folderPath) | System.IO.FileAttributes.System);

            // Create the .ini file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(iniFilePath))
            {
                file.WriteLine("[.ShellClassInfo]");
                file.WriteLine("IconResource=" + System.IO.Path.GetFileName(iconFilePath) + ",0");
            }

            // Set file attributes to make desktop.ini a system file and hidden
            System.IO.File.SetAttributes(iniFilePath, System.IO.File.GetAttributes(iniFilePath) | System.IO.FileAttributes.System); // | System.IO.FileAttributes.Hidden);

            // Refresh the folder to apply changes
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + folderPath + "\"");
        }


    }
}
