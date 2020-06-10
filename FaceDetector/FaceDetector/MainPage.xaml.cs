using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FaceDetector
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _uri = "FACE_API_URL";
        private const string _key = "SUBSCRIPTION_KEY";

        public MainPage()
        {
            this.InitializeComponent();

            this.SizeChanged += (s, args) =>
            {
                DeleteFaceRectangles();
            };
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
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    // Delete existing face rectangles
                    DeleteFaceRectangles();

                    // Show the image
                    var image = new BitmapImage();
                    await image.SetSourceAsync(stream);
                    LoadedImage.Source = image;

                    Container.UpdateLayout();
                    var ratio = LoadedImage.ActualHeight / image.PixelHeight;

                    Progress.IsActive = true;
                    Overlay.Visibility = Visibility.Visible;

                    try
                    {
                        // Submit the image to the Face API
                        FaceClient client = new FaceClient(
                            new ApiKeyServiceClientCredentials(_key),
                            new System.Net.Http.DelegatingHandler[] { }
                        );

                        client.Endpoint = _uri;

                        IList<FaceAttributeType> attributes = new FaceAttributeType[]
                        {
                            FaceAttributeType.Gender,
                            FaceAttributeType.Age,
                            FaceAttributeType.Emotion,
                            FaceAttributeType.Glasses,
                            FaceAttributeType.FacialHair
                        };

                        stream.Seek(0L);
                        var faces = await client.Face.DetectWithStreamAsync(stream.AsStream(), true, false, attributes);

                        Progress.IsActive = false;
                        Overlay.Visibility = Visibility.Collapsed;

                        foreach (var face in faces)
                        {
                            // Highlight the face with a Rectangle
                            var rect = new Rectangle();
                            rect.Width = face.FaceRectangle.Width * ratio;
                            rect.Height = face.FaceRectangle.Height * ratio;

                            var x = (face.FaceRectangle.Left * ratio) + ((Container.ActualWidth - LoadedImage.ActualWidth) / 2.0);
                            var y = (face.FaceRectangle.Top * ratio) + ((Container.ActualHeight - LoadedImage.ActualHeight) / 2.0);
                            rect.Margin = new Thickness(x, y, 0, 0);
                            rect.HorizontalAlignment = HorizontalAlignment.Left;
                            rect.VerticalAlignment = VerticalAlignment.Top;

                            rect.Fill = new SolidColorBrush(Colors.Transparent);
                            rect.Stroke = new SolidColorBrush(Colors.Red);
                            rect.StrokeThickness = 2.0;
                            rect.Tag = face.FaceId;

                            rect.PointerEntered += (s, args) =>
                            {
                                // Change the rectangle border to yellow when the pointer enters it
                                rect.Stroke = new SolidColorBrush(Colors.Yellow);
                            };

                            rect.PointerExited += (s, args) =>
                            {
                                // Change the rectangle border to red when the pointer exits it
                                rect.Stroke = new SolidColorBrush(Colors.Red);
                            };

                            rect.PointerPressed += async (s, args) =>
                            {
                                // Display information about a face when it is clicked
                                var id = (Guid)((Rectangle)s).Tag;
                                var selected = faces.Where(f => f.FaceId == id).First();

                                var gender = selected.FaceAttributes.Gender;
                                var age = selected.FaceAttributes.Age;
                                var beard = selected.FaceAttributes.FacialHair.Beard > 0.50 ? "Yes" : "No";
                                var moustache = selected.FaceAttributes.FacialHair.Moustache > 0.50 ? "Yes" : "No";
                                var glasses = selected.FaceAttributes.Glasses;

                                // Use reflection to enumerate Emotion properties
                                var props = selected.FaceAttributes.Emotion.GetType()
                                    .GetProperties()
                                    .Where(pi => pi.PropertyType == typeof(double) && pi.GetGetMethod() != null)
                                    .Select(pi => new
                                    {
                                        pi.Name,
                                        Value = (double)pi.GetGetMethod().Invoke(selected.FaceAttributes.Emotion, null)
                                    });

                                // Determine the dominant emotion
                                var max = props.Max(p => p.Value);
                                var emotion = props.Single(p => p.Value == max).Name;

                                // Show the results
                                var message = $"Gender: {gender}\nAge: {age}\nBeard: { beard}\nMoustache: {moustache}\nGlasses: {glasses}\nEmotion: {emotion}";
                                await new MessageDialog(message).ShowAsync();
                            };

                            Container.Children.Add(rect);
                        }
                    }
                    catch (Exception ex)
                    {
                        Progress.IsActive = false;
                        Overlay.Visibility = Visibility.Collapsed;
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

        private void DeleteFaceRectangles()
        {
            while (Container.Children.Count > 1)
            {
                Container.Children.RemoveAt(1);
            }
        }
    }
}
