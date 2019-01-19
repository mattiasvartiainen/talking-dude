// $env:GOOGLE_APPLICATION_CREDENTIALS="C:\Users\Mattias Vartiainen\source\repos\TextToSpeech\TextToSpeech\Assets\My-Project-f5e637856929.json"
using System;
using System.Threading.Tasks;

namespace TextToSpeech
{
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Windows.Devices.Enumeration;
    using Windows.Foundation;
    using Windows.Media;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;
    using Windows.Media.MediaProperties;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Google.Cloud.Speech.V1;
    using Google.Cloud.TextToSpeech.V1;
    using Windows.UI.Popups;

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public class TranscriptReceivedEventArgs : EventArgs
    {
        public string Transcript { get; set; }
    }

    public class CommunicationManagerGoogle : ICommunicationManager

    {
        SpeechClient _speech;
        private string _lastTranscript = string.Empty;
        SpeechClient.StreamingRecognizeStream _streamingCall;
        private bool _isRecording = false;
        private MediaCapture _mediaCapture = null;
        public event EventHandler<TranscriptReceivedEventArgs> TranscriptReceived;

        public CommunicationManagerGoogle()
        {
            Task.Run(SetupSpeechClient);
        }

        public bool IsRecording => _isRecording;

        public async Task<byte[]> TranslateTextToWav(string text)
        {
            // Instantiate a client
            var client = TextToSpeechClient.Create();

            // Set the text input to be synthesized.
            var input = new SynthesisInput
            {
                Text = text
            };

            // Build the voice request, select the language code ("en-US"),
            // and the SSML voice gender ("neutral").
            var voice = new VoiceSelectionParams
            {
                LanguageCode = "sv-SE",
                SsmlGender = SsmlVoiceGender.Female
            };

            // Select the type of audio file you want returned.
            var config = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Linear16
            };

            // Perform the Text-to-Speech request, passing the text input
            // with the selected voice parameters and audio file type
            var response = client.SynthesizeSpeech(new SynthesizeSpeechRequest
            {                
                Input = input,
                Voice = voice,
                AudioConfig = config
            });

            var memStream = new System.IO.MemoryStream();
            response.AudioContent.WriteTo(memStream);
            return memStream.ToArray();
        }

        static double ToDb(double value)
        {
            return 20 * Math.Log10(Math.Sqrt(value * 2));
        }

        void AmplitudeReading(object sender, double reading)
        {
            // Debug.WriteLine("Noise level: {0:0} dB", ToDb(reading));
        }

        public async Task StopRecording()
        {
            if (_mediaCapture == null) return;

            await _mediaCapture.StopRecordAsync();

            await _streamingCall.WriteCompleteAsync();

            _isRecording = false;
        }

        public string GetLastTranscript()
        {
            return _lastTranscript;
        }

        private async Task SetupSpeechClient()
        {
            _speech = SpeechClient.Create();
            _streamingCall = _speech.StreamingRecognize();

            _mediaCapture = await GetMediaCapture();

            await _streamingCall.WriteAsync(
                new StreamingRecognizeRequest()
                {
                    StreamingConfig = new StreamingRecognitionConfig()
                    {
                        Config = new RecognitionConfig()
                        {
                            Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = 16000,
                            LanguageCode = "sv",
                        },
                        InterimResults = true,
                    }
                });
        }

        public async Task<object> StreamingMicRecognizeAsync(int seconds)
        {
            if (_mediaCapture == null)
            {
                Debug.WriteLine("No microphone!");
                return null;
            }

            _isRecording = true;

            Task printResponses = PrintResponses();

            var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
            profile.Audio.SampleRate = (uint)16000; // Samples per second
            profile.Audio.BitsPerSample = (uint)16; // bits per sample
            profile.Audio.ChannelCount = (uint)1; // channels

            var stream = new AudioAmplitudeStream(_streamingCall); // custom stream implementation
            await _mediaCapture.StartRecordToStreamAsync(profile, stream);
            stream.AmplitudeReading += AmplitudeReading; // get an amplitude event

            await Task.Delay(TimeSpan.FromSeconds(seconds));
            if (_isRecording)
            {
                try
                {
                    await _mediaCapture.StopRecordAsync();
                    await _streamingCall.WriteCompleteAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }

            await printResponses;

            _isRecording = false;
            return null;
        }

        private Task PrintResponses()
        {
            return Task.Run(async () =>
            {
                while (await _streamingCall.ResponseStream.MoveNext(
                    default(CancellationToken)))
                {
                    foreach (var result in _streamingCall.ResponseStream
                        .Current.Results)
                    {
                        foreach (var alternative in result.Alternatives)
                        {
                            Debug.WriteLine(alternative.Transcript + " " + alternative.Confidence + " " + alternative.Words);
                            if (!(alternative.Confidence > 0.8)) continue;

                            _lastTranscript = alternative.Transcript;

                            // Send event
                            var handler = TranscriptReceived;
                            handler?.Invoke(this, new TranscriptReceivedEventArgs()
                            {
                                Transcript = _lastTranscript
                            });
                        }
                    }
                }
            });
        }

        private async Task<MediaCapture> GetMediaCapture()
        {
            var mics = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);

            var mediaCapture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings()
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                //AudioDeviceId = mics.First().Id
            };
            await mediaCapture.InitializeAsync(settings);

            mediaCapture.Failed += (_, ex) => Debug.WriteLine(ex.Message);

            var audioFrameSources = mediaCapture.FrameSources.Where(x => x.Value.Info.MediaStreamType == MediaStreamType.Audio);

            if (audioFrameSources.Count() == 0)
            {
                Debug.WriteLine("No audio frame source was found.");
                return null;
            }

            var frameSource = audioFrameSources.FirstOrDefault().Value;

            var format = frameSource.CurrentFormat;
            if (format.Subtype != MediaEncodingSubtypes.Float)
            {
                return null;
            }

            return mediaCapture;
        }
    }
}
