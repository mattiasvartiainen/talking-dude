namespace TextToSpeech
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Data;
    using System.Diagnostics;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Foundation;
    using Windows.Storage.Streams;
    using Google.Cloud.Speech.V1;

    public class AudioAmplitudeStream : IRandomAccessStream
    {
        private readonly SpeechClient.StreamingRecognizeStream _streamingCall;
        private object writeLock;

        public AudioAmplitudeStream(SpeechClient.StreamingRecognizeStream streamingCall)
        {
            writeLock = new object();

            _streamingCall = streamingCall;
        }

        public bool CanRead
        {
            get { return false; }
        }

        public bool CanWrite
        {
            get { return true; }
        }

        public IRandomAccessStream CloneStream()
        {
            throw new NotImplementedException();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotImplementedException();
        }

        public ulong Position
        {
            get { return 0; }
        }

        public void Seek(ulong position)
        {

        }

        public ulong Size
        {
            get
            {
                return 0;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void Dispose()
        {

        }

        public Windows.Foundation.IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            throw new NotImplementedException();
        }

        public Windows.Foundation.IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {

            return AsyncInfo.Run<uint, uint>((token, progress) =>
            {
                return Task.Run(() =>
                    {
                    using (var memoryStream = new MemoryStream())
                    using (var outputStream = memoryStream.AsOutputStream())
                    {
                        outputStream.WriteAsync(buffer).AsTask().Wait();

                        var byteArray = memoryStream.ToArray();
                        var enumerable = Decode(byteArray);

                        var list = new List<short>();
                        foreach (var e in enumerable)
                        {
                            list.Add((short)e);
                        }

                            lock (writeLock)
                        {

                            Debug.WriteLine("Send StreamingRecognizeRequest");
                            _streamingCall.WriteAsync(
                                new StreamingRecognizeRequest()
                                {
                                    AudioContent = Google.Protobuf.ByteString.CopyFrom(byteArray, 0, byteArray.Length)
                                }).Wait();
                        }

                        var amplitude = list.Select(Math.Abs).Average(x => x);

                        if (AmplitudeReading != null) this.AmplitudeReading(this, amplitude);

                        progress.Report((uint)memoryStream.Length);
                        return (uint)memoryStream.Length;
                    }
                });
            });
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return AsyncInfo.Run<bool>(_ => Task.Run(() => true));
        }

        private IEnumerable Decode(byte[] byteArray)
        {
            for (var i = 0; i < byteArray.Length - 1; i += 2)
            {
                yield return (BitConverter.ToInt16(byteArray, i));
            }
        }

        public delegate void AmplitudeReadingEventHandler(object sender, double reading);

        public event AmplitudeReadingEventHandler AmplitudeReading;

    }
}