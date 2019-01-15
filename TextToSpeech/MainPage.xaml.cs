using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TextToSpeech
{
    using System.Diagnostics;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Windows.UI.Xaml.Media.Imaging;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CommunicationManager communicationManager;

        public MainPage()
        {
            this.InitializeComponent();

            string host = "https://westeurope.tts.speech.microsoft.com/cognitiveservices/v1";
            communicationManager = new CommunicationManager(host);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var text = "Hej jag heter Fisken och äter blåbär";

            var wav = await communicationManager.TranslateTextToWav(text);
            var soundSource = ConvertTo(wav);
            soundSource.Seek(0);

            SoundPlayer.SetSource(soundSource, "audio/wav");
            //SoundPlayer.Play();
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
            var svar = "Jag förstår inte frågan";

            if (Question.Text == "Vad heter du?")
                svar = "Jag heter Albin";

            if (Question.Text == "Hur gammal är du?")
                svar = "Jag är 12 år";

            Say(svar);

            RemoveQuestion();
        }

        private void RemoveQuestion()
        {
            Question.Text = string.Empty;
        }
    }
}
