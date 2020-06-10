using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TweetAnalyzer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _key = "subscription_key";
        private const string _uri = "sentiment_url";
        private const string _consumerKey = "twitter_consumer_key";
        private const string _consumerSecret = "twitter_consumer_secret";
        private const string _accessToken = "twitter_access_token";
        private const string _accessTokenSecret = "twitter_access_token_secret";

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnAnalyzeButtonClicked(object sender, RoutedEventArgs e)
        {
            Progress.IsActive = true;
            Overlay.Visibility = Visibility.Visible;
            Output.Items.Clear();

            try
            {
                var hashtag = Hashtag.Text;
                if (!hashtag.StartsWith("#"))
                    hashtag = "#" + hashtag;

                // Get up to 99 tweets
                Auth.SetUserCredentials(_consumerKey, _consumerSecret, _accessToken, _accessTokenSecret);
                var parameters = new SearchTweetsParameters(hashtag);
                //parameters.SearchType = SearchResultType.Recent;
                parameters.SearchType = SearchResultType.Mixed;
                parameters.MaximumNumberOfResults = 99;
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

                // Clean the tweets, show them in the ListBox, and package them
                // for the Text Analytics API
                int id = 1;
                List<MultiLanguageInput> input = new List<MultiLanguageInput>();

                foreach (var tweet in tweets)
                {
                    var text = Regex.Replace(tweet.Text, @"http[^\s]+", "").Replace("\"", "\\\"").Replace("\n", " ");
                    input.Add(new MultiLanguageInput((id++).ToString(), text));
                    Output.Items.Add(text);
                }

                var batch = new MultiLanguageBatchInput(input);

                // Analyze the tweets for sentiment
                TextAnalyticsClient client = new TextAnalyticsClient(
                    new ApiKeyServiceClientCredentials(_key),
                    new System.Net.Http.DelegatingHandler[] { }
                );

                client.Endpoint = _uri;
                var results = await client.SentimentBatchAsync(batch);

                Progress.IsActive = false;
                Overlay.Visibility = Visibility.Collapsed;

                // Show the average sentiment score for all tweets
                var score = results.Documents.Select(x => x.Score).Average();
                await new MessageDialog($"Sentiment: {score:F2}").ShowAsync();
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