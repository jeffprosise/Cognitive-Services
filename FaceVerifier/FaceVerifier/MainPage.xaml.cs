using Microsoft.ProjectOxford.Face;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FaceVerifier
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string _key = "subscription_key";

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnVerifyButtonClicked(object sender, RoutedEventArgs e)
        {
            var client = new FaceServiceClient(_key);

            // Get a face ID for the face in the reference image
            var faces = await client.DetectAsync("https://prosise.blob.core.windows.net/photos/JeffProsise.jpg");

            if (faces.Length == 0)
            {
                await new MessageDialog("No faces detected in reference photo").ShowAsync();
                return;
            }

            Guid id1 = faces[0].FaceId;

            // Capture a photo of the person in front of the camera
            CameraCaptureUI capture = new CameraCaptureUI();
            capture.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            var file = await capture.CaptureFileAsync(CameraCaptureUIMode.Photo);

            using (var stream = await file.OpenStreamForReadAsync())
            {
                // Get a face ID for the face in the photo
                faces = await client.DetectAsync(stream);

                if (faces.Length == 0)
                {
                    await new MessageDialog("No faces detected in photo").ShowAsync();
                    return;
                }

                Guid id2 = faces[0].FaceId;

                // Compare the two faces
                var response = await client.VerifyAsync(id1, id2);
                await new MessageDialog("Probability that you are Jeff Prosise: " + response.Confidence.ToString()).ShowAsync();
            }

            // Delete the storage file
            await file.DeleteAsync();
        }
    }
}
