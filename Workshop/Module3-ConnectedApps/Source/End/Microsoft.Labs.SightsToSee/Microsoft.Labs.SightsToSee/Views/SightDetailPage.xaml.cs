﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using InkToolbarPreview;
using Microsoft.Labs.SightsToSee.Controls;
using Microsoft.Labs.SightsToSee.Services.FileService;
using Microsoft.Labs.SightsToSee.ViewModels;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using Windows.System;
using Microsoft.Labs.SightsToSee.Library.Models;
using Microsoft.Labs.SightsToSee.Library.Services.DataModelService;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Microsoft.Labs.SightsToSee.Views
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SightDetailPage : Page
    {
        private readonly InkPresenter _imageInkPresenter;

        #region InkToolBar

        private readonly InkBitmapRenderer _inkBitmapRenderer = new InkBitmapRenderer();

        #endregion

        private readonly InkRecognizerContainer _inkRecognizerContainer;

        public SightDetailPage()
        {
            InitializeComponent();

            NotesInkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse |
                                                           CoreInputDeviceTypes.Pen |
                                                           CoreInputDeviceTypes.Touch;
            _inkRecognizerContainer = new InkRecognizerContainer();
 
            #region InkToolbarControl

            _imageInkPresenter = ImageInkCanvas.InkPresenter;
            _imageInkPresenter.InputDeviceTypes =
                CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch | CoreInputDeviceTypes.Pen;

            #endregion
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string)
            {
                await ViewModel.LoadSightAsync(Guid.ParseExact((string) e.Parameter, "D"));
                //SightDetailControl.BackgroundImage = ViewModel.CurrentSight.ImageUri;
                await SetupNotesInkAsync();
            }
            base.OnNavigatedTo(e);
        }

        public async Task SetupNotesInkAsync()
        {
            // Check to see if we are in ink mode
            if (ViewModel.CurrentSight.NotesAreInk)
            {
                ViewModel.IsNotesInking = true;

                if (!string.IsNullOrWhiteSpace(ViewModel.CurrentSight.InkFileUri))
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(ViewModel.CurrentSight.InkFileUri));
                    using (var stream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        await NotesInkCanvas.InkPresenter.StrokeContainer.LoadAsync(stream);
                    }
                }
            }
        }

        private async void TryInkReco(object sender, RoutedEventArgs e)
        {
            var result = await InkRecoDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.IsNotesInking = false;
                ViewModel.CurrentSight.NotesAreInk = false;
                // The last note is followed by a space.
                ViewModel.CurrentSight.Notes += $" {Status.Text}";
                await ViewModel.UpdateSightAsync(ViewModel.CurrentSight);
                Status.Text = string.Empty;
                NotesInkCanvas.InkPresenter.StrokeContainer.Clear();
            }
        }

        private async void InkRecoDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            var currentStrokes =
                NotesInkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                var recognitionResults = await _inkRecognizerContainer.RecognizeAsync(
                    NotesInkCanvas.InkPresenter.StrokeContainer,
                    InkRecognitionTarget.All);

                if (recognitionResults.Count > 0)
                {
                    // Display recognition result
                    var str = string.Empty;
                    foreach (var r in recognitionResults)
                    {
                        str += $"{r.GetTextCandidates()[0]} ";
                    }
                    Status.Text = str;
                    InkRecoDialog.IsPrimaryButtonEnabled = true;
                }
                else
                {
                    Status.Text = "No text recognized.";
                }
            }
            else
            {
                Status.Text = "Must first write something.";
            }
        }


        private async void NotesSaveButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CurrentSight.NotesAreInk = true;

            var sightFile = await ViewModel.CreateSightFileAndAssociatedStorageFileAsync();

            // In the Sight, record where the ink strokes file can be found
            ViewModel.CurrentSight.InkFileUri = sightFile.Uri;

            // Get the destination StorageFile
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(sightFile.Uri));

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await NotesInkCanvas.InkPresenter.StrokeContainer.SaveAsync(stream);
            }

            // save the file with the ink strokes
            await ViewModel.SaveSightFileAsync(sightFile);
            // and update the associated record
            await ViewModel.UpdateSightAsync(ViewModel.CurrentSight);
        }

        private async void NotesUndoButton_Click(object sender, RoutedEventArgs e)
        {
            NotesInkCanvas.InkPresenter.StrokeContainer.Clear();
            ViewModel.CurrentSight.NotesAreInk = false;
            ViewModel.IsNotesInking = false;
            await ViewModel.UpdateSightAsync(ViewModel.CurrentSight);
        }

        private async void OnLaunchForResults(object sender, RoutedEventArgs e)
        {
            // Insert the M3_OpenPicker snippet here

            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");

            StorageFile file = await openPicker.PickSingleFileAsync();

            // Insert the M3_LaunchForResults snippet here

            if (file != null)
            {
                //Send data to the service 
                var token = SharedStorageAccessManager.AddFile(file);

                var message = new ValueSet();

                //For QuickStart
                message.Add("Token", token);

                var targetAppUri = new Uri("lumiaphotoeditingquick:");

                // We want a specific app to perform our photo editing operation, not just any that implements the protocol we're using for launch
                var options = new LauncherOptions();
                options.TargetApplicationPackageFamilyName = "3ac26f24-3747-47ef-bfc5-b877b482f0f3_gd40dm16kn5w8";

                var response = await Launcher.LaunchUriForResultsAsync(targetAppUri, options, message);

                if (response.Status == LaunchUriStatus.Success)
                {
                    if (response.Result != null)
                    {
                        string alteredFileToken = response.Result["Token"].ToString();
                        var alteredFile = await SharedStorageAccessManager.RedeemTokenForFileAsync(alteredFileToken);

                        // get the destination where this file needs to go
                        var sightFile = await ViewModel.CreateSightFileAndAssociatedStorageFileAsync();

                        // Get the destination StorageFile
                        var destFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(sightFile.Uri));

                        // save the edited image file at the required destination
                        await alteredFile.CopyAndReplaceAsync(destFile);
                        sightFile.FileType = SightFileType.ImageGallery;

                        await ViewModel.SaveSightFileAsync(sightFile);
                    }
                }
            }
        }


        #region InkToolBarControl

        private async void ImageSaveButton_Click(object sender, RoutedEventArgs e)
        {
            var renderedImage = await _inkBitmapRenderer.RenderAsync(
                _imageInkPresenter.StrokeContainer.GetStrokes(),
                ImageInkCanvas.ActualWidth,
                ImageInkCanvas.ActualHeight);

            if (renderedImage != null)
            {
                // Convert to a format appropriate for SoftwareBitmapSource.
                var convertedImage = SoftwareBitmap.Convert(
                    renderedImage,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied
                    );
                await InkImageSource.SetBitmapAsync(convertedImage);

                var renderTargetBitmap = new RenderTargetBitmap();
                var currentDpi = DisplayInformation.GetForCurrentView().LogicalDpi;

                // Prepare for RenderTargetBitmap by hiding the InkCanvas and displaying the
                // rasterized strokes instead.
                ImageInkCanvas.Visibility = Visibility.Collapsed;
                InkImage.Visibility = Visibility.Visible;

                await renderTargetBitmap.RenderAsync(InkingRoot);
                var pixelData = await renderTargetBitmap.GetPixelsAsync();

                // Restore the original layout now that we have created the RenderTargetBitmap image.
                ImageInkCanvas.Visibility = Visibility.Visible;
                InkImage.Visibility = Visibility.Collapsed;

                // Create destination file for the new image
                var destFolder = await ViewModel.GetStorageFolderForSightFile(ViewModel.SelectedSightFile.Id);
                var file = await destFolder.CreateFileAsync($"{Guid.NewGuid().ToString("D")}.png",
                        CreationCollisionOption.GenerateUniqueName);

                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Ignore,
                        (uint)renderTargetBitmap.PixelWidth,
                        (uint)renderTargetBitmap.PixelHeight,
                        currentDpi,
                        currentDpi,
                        pixelData.ToArray()
                        );

                    await encoder.FlushAsync();
                }

                // Update the SightFile in the database
                await ViewModel.UpdateSightFileImageUriAsync(file.GetUri());

                // Erase all strokes.
                ImageInkCanvas.InkPresenter.StrokeContainer.Clear();
            }
        }

        private void EraserClearAll(InkToolbar sender, object args)
        {
            // Erase all strokes.
            sender.TargetInkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private async void ImageUndoButton_Click(object sender, RoutedEventArgs e)
        {
            // Erase all strokes.
            ImageInkCanvas.InkPresenter.StrokeContainer.Clear();

            await ViewModel.RevertSightFileToOriginalUriAsync();
        }

        #endregion



        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Ensure NotesInkCanvas isn't locked
            NoteInkToolbar.TargetInkCanvas = null;
            NotesInkCanvas = null;
            NoteInkToolbar = null;

            // save any changes to the notes
            await ViewModel.UpdateSightAsync(ViewModel.CurrentSight);
            base.OnNavigatedFrom(e);
        }

        public SightDetailPageViewModel ViewModel => DataContext as SightDetailPageViewModel;

        private void DeleteSightButton_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            MobileDeleteFlyout?.Flyout?.Hide();
            DesktopDeleteFlyout?.Flyout?.Hide();
        }

    }
}