﻿using Microsoft.Win32;
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
using FolderCustomizer.Editor;

namespace FolderCustomizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select FOLDER/DIRECTORY to customize
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select a folder to customize";
            folderBrowserDialog.ShowDialog();
            updateSelectedFolderTxt(folderBrowserDialog.SelectedPath);

            updateCanvas(folderBrowserDialog.SelectedPath);




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
            Image folderIcon = new Image();
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
    }


}