namespace TextToSpeech
{
    using System;
    using System.Threading.Tasks;

    public interface ICommunicationManager
    {
        Task<byte[]> TranslateTextToWav(string body);

        Task<object> StreamingMicRecognizeAsync(int seconds);

        bool IsRecording { get; }

        Task StopRecording();

        string GetLastTranscript();

        event EventHandler<TranscriptReceivedEventArgs> TranscriptReceived;
    }
}