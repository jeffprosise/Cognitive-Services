using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NotHotDog
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _uri = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/65224b4f-17ea-4467-86c8-8ddef052f9db/image";
        private const string _key = "73afaeffbdad43b1b8bb05628a60f73c";

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnSelectImageButtonClicked(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                byte[] buffer;

                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    // Show the image
                    var image = new BitmapImage();
                    await image.SetSourceAsync(stream);
                    LoadedImage.Source = image;

                    // Read the image into a byte array
                    stream.Seek(0L);
                    var bytes = new byte[stream.Size];
                    await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
                    buffer = bytes;
                }

                try
                {
                    Progress.IsActive = true;
                    Overlay.Visibility = Visibility.Visible;

                    // Submit the image to the Custom Vision Service
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Prediction-Key", _key);
                    var content = new ByteArrayContent(buffer);

                    var response = await client.PostAsync(_uri, content);

                    Progress.IsActive = false;
                    Overlay.Visibility = Visibility.Collapsed;

                    if (response.IsSuccessStatusCode)
                    {
                        // Show the results
                        var json = await response.Content.ReadAsStringAsync();
                        dynamic result = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                        dynamic prediction = ((IEnumerable<dynamic>)(result.Predictions)).Where(p => p.Tag == "HotDog").ToArray()[0];
                        var probability = prediction.Probability;

                        if (probability > 0.90)
                        {
                            await new MessageDialog("It's a hot dog!").ShowAsync();
                        }
                        else
                        {
                            await new MessageDialog("Not a hot dog").ShowAsync();
                        }
                    }
                    else
                    {
                        await new MessageDialog($"Call failed ({response.StatusCode})").ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    await new MessageDialog(ex.Message).ShowAsync();
                }
                finally
                {
                    Progress.IsActive = false;
                    Overlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
