using Microsoft.ProjectOxford.SpeakerRecognition;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SpeakerVerifier
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Guid _id;
        private string _key = "subscription_key";
        private InMemoryRandomAccessStream _buffer;
        private MediaCapture _capture;
        private bool _recording = false;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Get the speaker profile
            var client = new SpeakerVerificationServiceClient(_key);
            var profiles = await client.GetProfilesAsync();

            if (profiles.Length == 0)
            {
                await new MessageDialog("No speaker profiles found for this subscription", "Error").ShowAsync();
                return;
            }

            _id = profiles[0].ProfileId;
        }

        private async void OnButtonClicked(object sender, RoutedEventArgs e)
        {
            if (!_recording)
            {
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Audio
                };

                _capture = new MediaCapture();
                await _capture.InitializeAsync(settings);

                _capture.RecordLimitationExceeded += async (MediaCapture s) =>
                {
                    await new MessageDialog("Record limtation exceeded", "Error").ShowAsync();
                };

                _capture.Failed += async (MediaCapture s, MediaCaptureFailedEventArgs args) =>
                {
                    await new MessageDialog("Media capture failed: " + args.Message, "Error").ShowAsync();
                };

                _buffer = new InMemoryRandomAccessStream();
                var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
                profile.Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16); // Must be mono (1 channel)
                await _capture.StartRecordToStreamAsync(profile, _buffer);

                TheButton.Content = "Verify";
                _recording = true;
            }
            else // Recording
            {
                if (_capture != null && _buffer != null && _id != null && _id != Guid.Empty)
                {
                    await _capture.StopRecordAsync();
                    IRandomAccessStream stream = _buffer.CloneStream();
                    var client = new SpeakerVerificationServiceClient(_key);

                    var response = await client.VerifyAsync(stream.AsStream(), _id);

                    string message = String.Format("Result: {0}, Confidence: {1}", response.Result, response.Confidence);
                    await new MessageDialog(message).ShowAsync();

                    _capture.Dispose();
                    _capture = null;

                    _buffer.Dispose();
                    _buffer = null;
                }

                TheButton.Content = "Start";
                _recording = false;
            }
        }
    }
}
