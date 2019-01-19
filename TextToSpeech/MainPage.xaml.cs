

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TextToSpeech
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Windows.ApplicationModel.Core;
    using Windows.Storage.Streams;
    using Windows.UI.Core;
    using Windows.UI.Core.Preview;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;

    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly ICommunicationManager communicationManager;

        public MainPage()
        {
            var useGoogle = true;

            InitializeComponent();

            var host = "https://westeurope.tts.speech.microsoft.com/cognitiveservices/v1";

            communicationManager = useGoogle
                ? new CommunicationManagerGoogle()
                : (ICommunicationManager) new CommunicationManagerAzure(host);
            communicationManager.TranscriptReceived += CommunicationManager_TranscriptReceived;

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested +=
                async (sender, args) =>
                {
                    args.Handled = true;
                    Application.Current.Exit();
                };
        }

        private void CommunicationManager_TranscriptReceived(object sender, TranscriptReceivedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RecordedQuestion.Text = e.Transcript;
                AnswerQuestion(e.Transcript);
            });
            //var text = communicationManager.GetLastTranscript();
            //Say(text);
        }

        private async void Say(string text)
        {
            var wav = await communicationManager.TranslateTextToWav(text);
            var soundSource = ConvertTo(wav);
            soundSource.Seek(0);

            SoundPlayer.SetSource(soundSource, "audio/wav");
        }


        private static IRandomAccessStream ConvertTo(byte[] arr)
        {
            return arr.AsBuffer().AsStream().AsRandomAccessStream();
        }

        private void SoundPlayer_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            Cartman.Stop();
            RecordedQuestion.Text = string.Empty;
            Question.Text = string.Empty;
        }

        private void SoundPlayer_OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Cartman.Play();
        }

        private void SoundPlayer_OnCurrentStateChanged(object sender, RoutedEventArgs e)
        {
        }

        private void WhenQuestion(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Question.Text))
                return;

            AnswerQuestion(Question.Text.Trim('?'));
        }

        private void AnswerQuestion(string question)
        {
            var svar = "Jag förstår inte frågan";

            if (question == "Vad heter du")
                svar = "Jag heter Jöns Petter Svanström";

            if (question == "Hur gammal är du")
                svar = "Jag är 12 år";

            if (question == "Vad gillar du för mat")
                svar = "Jag tycker mycket om spagetti och köttfärsås";

            if (question == "Vad gillar du för tv program")
                svar = "Jag gillar kung julien";

            Say(svar);
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (communicationManager.IsRecording)
            {
                await communicationManager.StopRecording();
                Microphone.Source = new BitmapImage(new Uri("ms-appx:///Assets/microphone.png"));
            }
            else
            {
                Microphone.Source = new BitmapImage(new Uri("ms-appx:///Assets/microphone_red.png"));
                await communicationManager.StreamingMicRecognizeAsync(10);
            }
        }
    }
}