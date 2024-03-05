using FolderCustomizer.Editor;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;
using Size = System.Windows.Size;

namespace FolderCustomizer
{
    public partial class MainWindow : Window
    {

        private System.Windows.Controls.Image folderIcon;

        public MainWindow()
        {
            InitializeComponent();

            // adding the different baseIcons to the combobox
            cbx_Bases.Items.Add("EmptyFolder");
            cbx_Bases.Items.Add("FullFolder");


            // set the default colour to yellow
            cbx_Bases.SelectedIndex = 0;
        }

        private string folderPath = "";

        private void Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select FOLDER/DIRECTORY to customize
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select a folder to customize";
            folderBrowserDialog.ShowDialog();

            // If the selectedPath is not empty, then update the selected folder textbox and the canvas
            if (folderBrowserDialog.SelectedPath != "")
            {
                updateSelectedFolderTxt(folderBrowserDialog.SelectedPath);

                updateCanvas(folderBrowserDialog.SelectedPath);

                folderPath = folderBrowserDialog.SelectedPath;
            }
            
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

            // Create the base icon
            folderIcon = new System.Windows.Controls.Image();
            string baseIcon = cbx_Bases.SelectedItem.ToString().ToLower();
            folderIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/res/images/bases/{baseIcon}.png"));
            folderIcon.Width = canvas.Width;
            folderIcon.Height = canvas.Height;
            canvas.Children.Add(folderIcon);
        }

        private void Cbx_ColourChanged(object sender, SelectionChangedEventArgs e)
        {
            updateCanvas(folderPath);
        }

        private void Btn_AddImage_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select IMAGE to add to the canvas
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg";
            openFileDialog.ShowDialog();

            // Add an EditableImageCanvas to the iconEditorCanvas
            Canvas canvas = iconEditorCanvas;
            try
            {
                EditableImageCanvas imageEditable = new EditableImageCanvas(new Uri(openFileDialog.FileName));
                canvas.Children.Add(imageEditable);
            }
            // ignore if user cancels the file dialog
            catch (Exception ex) when (ex is System.ArgumentException || ex is System.UriFormatException)
            {
                return;
            }
            // show error message if there is an error
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void Btn_ColourPicker_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select COLOUR to add to the canvas
            System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
            colorDialog.ShowDialog();

            // Get the selected color
            System.Drawing.Color selectedColor = colorDialog.Color;

            // Convert the System.Drawing.Color to System.Windows.Media.Color
            System.Windows.Media.Color wpfColor = System.Windows.Media.Color.FromArgb(selectedColor.A, selectedColor.R, selectedColor.G, selectedColor.B);

            // Apply the color to the image
            ApplyColorToImage(wpfColor);
        }

        private void ApplyColorToImage(System.Windows.Media.Color color)
        {

            // img = the selection from combobox
            string baseIcon = cbx_Bases.SelectedItem.ToString().ToLower();
            BitmapImage img = new BitmapImage(new Uri($"pack://application:,,,/res/images/bases/{baseIcon}.png"));

            // Create a new WriteableBitmap from the original image source
            BitmapSource bitmapSource = img;
            WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapSource);

            // Lock the bitmap to write pixel data
            writeableBitmap.Lock();

            // Get the pixel buffer
            IntPtr buffer = writeableBitmap.BackBuffer;
            int stride = writeableBitmap.BackBufferStride;
            int width = writeableBitmap.PixelWidth;
            int height = writeableBitmap.PixelHeight;

            // Iterate through each pixel and apply the color shift
            unsafe
            {
                byte* p = (byte*)buffer.ToPointer();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Get the pixel offset
                        int offset = y * stride + 4 * x;

                        // Get the original pixel color
                        byte blue = p[offset];
                        byte green = p[offset + 1];
                        byte red = p[offset + 2];
                        byte alpha = p[offset + 3];

                        // Calculate grayscale intensity
                        double intensity = (0.299 * red + 0.587 * green + 0.114 * blue) / 255.0;

                        // Calculate new color components based on the selected color
                        byte newRed = (byte)(color.R * intensity);
                        byte newGreen = (byte)(color.G * intensity);
                        byte newBlue = (byte)(color.B * intensity);

                        // Update the pixel color
                        p[offset] = newBlue;
                        p[offset + 1] = newGreen;
                        p[offset + 2] = newRed;
                    }
                }
            }

            // Unlock the bitmap
            writeableBitmap.Unlock();

            // Update the image source with the modified bitmap
            folderIcon.Source = writeableBitmap;
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

            // If PNG already exists, delete it
            string pngFilePath = System.IO.Path.Combine(folderPath, "custom_icon.png");
            if (System.IO.File.Exists(pngFilePath))
            {
                System.IO.File.Delete(pngFilePath);
            }

            // Save the bitmap as a PNG file
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            using (var fileStream = new System.IO.FileStream(pngFilePath, System.IO.FileMode.Create))
            {
                encoder.Save(fileStream);
            }

            // Convert PNG to ICO
            string icoFilePath = System.IO.Path.Combine(folderPath, "custom_icon.ico");
            ImagingHelper.ConvertToIcon(pngFilePath, icoFilePath, 512);
            // Set the .png to hidden
            System.IO.File.SetAttributes(pngFilePath, System.IO.File.GetAttributes(pngFilePath) | System.IO.FileAttributes.Hidden);

            UpdateFolderIcon(icoFilePath, folderPath);
        }

        private void UpdateFolderIcon(string iconFilePath, string folderPath)
        {
            // update the .ini file to look like this:
            /*
             * [.ShellClassInfo]
             * IconResource=custom_icon.ico,0  
             */

            string iniFilePath = System.IO.Path.Combine(folderPath, "desktop.ini");

            // make the folder a system folder
            System.IO.File.SetAttributes(folderPath, System.IO.File.GetAttributes(folderPath) | System.IO.FileAttributes.System);

            // If the desktop.ini file exists, delete it
            if (System.IO.File.Exists(iniFilePath))
                System.IO.File.Delete(iniFilePath);

            // Create the .ini file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(iniFilePath))
            {
                file.WriteLine("[.ShellClassInfo]");
                file.WriteLine("IconResource=" + System.IO.Path.GetFileName(iconFilePath) + ",0");
            }

            // Set file attributes to make desktop.ini a system file and hidden
            System.IO.File.SetAttributes(iniFilePath, System.IO.File.GetAttributes(iniFilePath) | System.IO.FileAttributes.System | System.IO.FileAttributes.Hidden);

            // Set the .ico as hidden
            System.IO.File.SetAttributes(iconFilePath, System.IO.File.GetAttributes(iconFilePath) | System.IO.FileAttributes.Hidden);

            // Refresh the folder to apply changes
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + folderPath + "\"");
        }


    }
}
