// ----------------------------------------------------------------------
// <copyright file="MainPage.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// All rights reserved.
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
// KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
// </copyright>
// ----------------------------------------------------------------------
// <summary>MainPage.xaml.cs</summary>
// ----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Linq;
using Windows.UI.Xaml.Navigation;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Windows.UI.Popups;

/// <summary>
/// 
/// </summary>

namespace SpeechTranslator
{
    /// <summary>
    /// App to showcase the Machine Translation Translate Speech API and AudioGraph
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const string AzureSecretKey = "subscription_key";
        private Dictionary<string, List<ComboBoxItem>> langVoiceDict = new Dictionary<string, List<ComboBoxItem>>();

        public MainPage()
        {
            this.InitializeComponent();
            this.ViewModel = new ResultViewModel();
            this.speechTranslateClient = new SpeechTranslateClient(AzureSecretKey);
        }

        /// <summary>
        /// Populate the Mic and Speaker ComboBox
        /// </summary>
        /// <param name="e"></param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            foreach (var device in await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector()))
            {
                this.micBox.Items.Add(device);
            }

            foreach (var device in await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector()))
            {
                this.speakerBox.Items.Add(device);
            }

            this.micBox.SelectedIndex = 0;
            this.speakerBox.SelectedIndex = 0;

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("https://dev.microsofttranslator.com/languages?api-version=1.0&scope=text,tts,speech");
                // add header
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                dynamic jsonObject = JObject.Parse(jsonString);

                // Populate the "From" ComboxBox with sorted items
                var fromItems = new Dictionary<string, string>();

                foreach (var s in jsonObject.speech)
                {
                    fromItems.Add(s.Name, s.Value.name.ToString());
                }

                var fromItemsSorted = fromItems.OrderBy(i => i.Value);

                foreach (var item in fromItemsSorted)
                {
                    var cbi = new ComboBoxItem() { Name = item.Key, Content = item.Value };
                    this.fromComboBox.Items.Add(cbi);
                    if (item.Value.ToLower() == "english") // Default to English if present
                        this.fromComboBox.SelectedItem = cbi;
                }

                // Populate langVoiceDict for the "Voice" ComobBox
                foreach (var s in jsonObject.tts)
                {
                    string lang = s.Value.language;
                    if (!langVoiceDict.ContainsKey(lang))
                        langVoiceDict[lang] = new List<ComboBoxItem>();

                    langVoiceDict[lang].Add(new ComboBoxItem() { Name = s.Name, Content = String.Format("{0} ({1}) ({2})", s.Value.displayName, s.Value.gender, s.Value.regionName) });
                }

                // Populate the "To" ComboBox with sorted items, but only with languages supported by TTS
                var toItems = new Dictionary<string, string>();

                foreach (var s in jsonObject.text)
                {
                    if (langVoiceDict.ContainsKey(s.Name))
                        toItems.Add(s.Name, s.Value.name.ToString());
                }

                var toItemsSorted = toItems.OrderBy(i => i.Value);

                foreach (var item in toItemsSorted)
                {
                    var cbi = new ComboBoxItem() { Name = item.Key, Content = item.Value };
                    this.toComboBox.Items.Add(cbi);
                    if (item.Value.ToLower() == "french") // Default to French if present
                        this.toComboBox.SelectedItem = cbi;
                }
            }
        }

        /// <summary>
        /// Connect to the Machine Translation Service and Construct the Audio Graph
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await this.ViewModel.Clear();

            //this.statusText.Text = "Connecting to Speech Translate Service";

            // The service takes 2 callbacks Action<Result> for text recognition and translation
            // and Action<AudioFrame> for text to speech response
            var fromValue = (this.fromComboBox.SelectedValue as ComboBoxItem).Name;
            var toValue = (this.toComboBox.SelectedValue as ComboBoxItem).Name;
            var voiceValue = this.voiceComboBox.SelectedValue == null ? null : (this.voiceComboBox.SelectedValue as ComboBoxItem).Name;

            try
            {
                await this.speechTranslateClient.Connect(fromValue, toValue, voiceValue, this.DisplayResult, this.SendAudioOut);

                //this.statusText.Text = "Creating AudioGraph";

                var pcmEncoding = AudioEncodingProperties.CreatePcm(16000, 1, 16);

                // Construct the audio graph
                // mic -> Machine Translate Service
                // Machine Translation text to speech output -> speaker
                var result = await AudioGraph.CreateAsync(new AudioGraphSettings(AudioRenderCategory.Speech)
                {
                    DesiredRenderDeviceAudioProcessing = AudioProcessing.Raw,
                    AudioRenderCategory = AudioRenderCategory.Speech,
                    EncodingProperties = pcmEncoding
                });

                if (result.Status == AudioGraphCreationStatus.Success)
                {
                    this.graph = result.Graph;

                    #region input
                    // mic -> machine translation speech translate
                    var microphone = await DeviceInformation.CreateFromIdAsync(((DeviceInformation)this.micBox.SelectedValue).Id);

                    this.speechTranslateOutputMode = this.graph.CreateFrameOutputNode(pcmEncoding);
                    this.graph.QuantumProcessed += (s, a) => this.SendToSpeechTranslate(this.speechTranslateOutputMode.GetFrame());

                    this.speechTranslateOutputMode.Start();

                    var micInputResult = await this.graph.CreateDeviceInputNodeAsync(MediaCategory.Speech, pcmEncoding, microphone);

                    if (micInputResult.Status == AudioDeviceNodeCreationStatus.Success)
                    {
                        micInputResult.DeviceInputNode.AddOutgoingConnection(this.speechTranslateOutputMode);
                        micInputResult.DeviceInputNode.Start();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    #endregion

                    #region output
                    // machine translation text to speech output -> speaker

                    var speakerOutputResult = await this.graph.CreateDeviceOutputNodeAsync();

                    if (speakerOutputResult.Status == AudioDeviceNodeCreationStatus.Success)
                    {
                        this.speakerOutputNode = speakerOutputResult.DeviceOutputNode;
                        this.speakerOutputNode.Start();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }

                    this.textToSpeechOutputNode = this.graph.CreateFrameInputNode(pcmEncoding);
                    this.textToSpeechOutputNode.AddOutgoingConnection(this.speakerOutputNode);
                    this.textToSpeechOutputNode.Start();
                    #endregion

                    // start the graph
                    this.graph.Start();
                }

                //this.statusText.Text = "Ready";

                this.StartButton.IsEnabled = false;
                this.StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message).ShowAsync();
            }
        }

        /// <summary>
        /// display the recognition and translation result to the ViewModel
        /// </summary>
        /// <param name="result"></param>
        private async void DisplayResult(Result result)
        {
            await this.ViewModel.Add(result);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.ClearButton.IsEnabled = true;
            });
        }

        /// <summary>
        /// Send the audio result to the speaker output node.
        /// </summary>
        /// <param name="frame"></param>
        private void SendAudioOut(AudioFrame frame)
        {
            this.textToSpeechOutputNode.AddFrame(frame);
        }

        /// <summary>
        /// Send the data from the mic to the speech translate client
        /// </summary>
        /// <param name="frame"></param>
        private void SendToSpeechTranslate(AudioFrame frame)
        {
            this.speechTranslateClient.SendAudioFrame(frame);
        }

        AudioGraph graph;
        AudioFrameOutputNode speechTranslateOutputMode;
        AudioDeviceOutputNode speakerOutputNode;
        SpeechTranslateClient speechTranslateClient;
        AudioFrameInputNode textToSpeechOutputNode;

        /// <summary>
        /// reset the service
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.speechTranslateClient.Close();

            if (this.graph != null)
            {
                this.graph?.Stop();
                this.graph?.Dispose();
                this.graph = null;
            }

            this.StartButton.IsEnabled = true;
            this.StopButton.IsEnabled = false;
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            await this.ViewModel.Clear();
            this.ClearButton.IsEnabled = false;
        }

        /// <summary>
        /// ViewModel that is bind to the List View
        /// </summary>
        public ResultViewModel ViewModel { get; set; }

        private void toComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.voiceComboBox.Items.Clear();
            var item = this.toComboBox.SelectedValue as ComboBoxItem;

            if (this.langVoiceDict.ContainsKey(item.Name))
            {
                this.langVoiceDict[item.Name].ForEach(voice => this.voiceComboBox.Items.Add(voice));
                this.voiceComboBox.SelectedIndex = 0;
            }
        }
    }
}
