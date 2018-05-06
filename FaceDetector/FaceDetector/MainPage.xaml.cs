using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FaceDetector
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _uri = "face_api_url";
        private const string _key = "subscription_key";

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

                    try
                    {
                        Progress.IsActive = true;
                        Overlay.Visibility = Visibility.Visible;

                        // Submit the image to the Face API
                        var client = new FaceServiceClient(_key, _uri);

                        IEnumerable<FaceAttributeType> attributes = new FaceAttributeType[]
                        {
                            FaceAttributeType.Gender,
                            FaceAttributeType.Age,
                            FaceAttributeType.Emotion,
                            FaceAttributeType.Glasses,
                            FaceAttributeType.FacialHair
                        };

                        try
                        {
                            stream.Seek(0L);
                            var faces = await client.DetectAsync(stream.AsStream(), true, false, attributes); 
    
                            foreach(var face in faces)
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
                                        .Where(pi => pi.PropertyType == typeof(float) && pi.GetGetMethod() != null)
                                        .Select(pi => new
                                        {
                                            pi.Name,
                                            Value = pi.GetGetMethod().Invoke(selected.FaceAttributes.Emotion, null)
                                        });

                                    // Determine the dominant emotion
                                    float max = 0.0f;
                                    var emotion = String.Empty;

                                    foreach(var prop in props)
                                    {
                                        if ((float)prop.Value > max)
                                        {
                                            max = (float)prop.Value;
                                            emotion = prop.Name;
                                        }
                                    }

                                    // Show the results
                                    var message = $"Gender: {gender}\nAge: {age}\nBeard: { beard}\nMoustache: {moustache}\nGlasses: {glasses}\nEmotion: {emotion}";
                                    await new MessageDialog(message).ShowAsync();
                                };

                                Container.Children.Add(rect);
                            }
                        }
                        catch (FaceAPIException fex)
                        {
                            await new MessageDialog(fex.ErrorMessage).ShowAsync();
                        }
                        catch (Exception ex)
                        {
                            await new MessageDialog(ex.Message).ShowAsync();
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

        private void DeleteFaceRectangles()
        {
            while (Container.Children.Count > 1)
            {
                Container.Children.RemoveAt(1);
            }
        }
    }
}
