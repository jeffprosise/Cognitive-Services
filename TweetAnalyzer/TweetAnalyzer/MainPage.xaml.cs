using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TweetAnalyzer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _key = "subscription_key";
        private const string _consumerKey = "twitter_consumer_key";
        private const string _consumerSecret = "twitter_consumer_secret";
        private const string _accessToken = "twitter_access_token";
        private const string _accessTokenSecret = "twitter_access_token_secret";
        private const string _uri = "sentiment_url";

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnAnalyzeButtonClicked(object sender, RoutedEventArgs e)
        {
            Progress.IsActive = true;
            Output.Text = String.Empty;

            try
            {
                var hashtag = Hashtag.Text;
                if (!hashtag.StartsWith("#"))
                    hashtag = "#" + hashtag;

                // Get up to 40 tweets
                Auth.SetUserCredentials(_consumerKey, _consumerSecret, _accessToken, _accessTokenSecret);
                var parameters = new SearchTweetsParameters(hashtag);
                //parameters.SearchType = SearchResultType.Recent;
                parameters.SearchType = SearchResultType.Mixed;
                parameters.MaximumNumberOfResults = 40;
                IEnumerable<ITweet> tweets = null;

                await Task.Run(() =>
                {
                    tweets = Search.SearchTweets(parameters);
                });

                // Stop if no tweets were retrieved
                if (tweets.Count() == 0)
                {
                    await new MessageDialog("Hashtag not found", "Error").ShowAsync();
                    return;
                }

                // Extract the text from each tweet
                var builder = new StringBuilder();

                foreach (var tweet in tweets)
                {
                    // Remove URLs from tweets
                    builder.Append(Regex.Replace(tweet.Text, @"http[^\s]+", "") + "\n");
                }

                var text = builder.ToString();
                Output.Text = text;

                // Formulate JSON input
                dynamic document = new JObject();
                document.id = "1000";
                document.text = text.Replace("\"", "\\\""); // Escape quotation marks in tweets;
                dynamic container = new JObject();
                container.documents = new JArray(document);
                var json = container.ToString();

                // Call Cognitive Services
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);
                var content = new HttpStringContent(json);
                content.Headers.ContentType = new HttpMediaTypeHeaderValue("application/json");

                var response = await client.PostAsync(new Uri(_uri), content);
                Progress.IsActive = false;

                // Show the results
                if (response.IsSuccessStatusCode)
                {
                    json = await response.Content.ReadAsStringAsync();
                    var result = JObject.Parse(json);

                    if (result.documents.Count > 0)
                        await new MessageDialog("Sentiment: " + result.documents[0].score.ToString("F2")).ShowAsync();
                    else
                        await new MessageDialog(result.errors[0].message, "Error").ShowAsync();
                }
                else
                {
                    await new MessageDialog(response.ReasonPhrase).ShowAsync();
                }
            }
            finally
            {
                // Make sure the progress ring gets turned off
                Progress.IsActive = false;
            }
        }
    }
}
