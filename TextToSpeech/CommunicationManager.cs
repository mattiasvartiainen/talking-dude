using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextToSpeech
{
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using Windows.Storage;
    using Windows.Storage.Streams;

    public class CommunicationManager
    {
        private readonly string _host;
        private string _accessToken = null;

        private string AccessToken {
            get
            {
                if (_accessToken == null)
                    _accessToken = GetAccessToken().Result;

                return _accessToken;
            }
            set { _accessToken = value; }
        }

        public CommunicationManager(string host)
        {
            _host = host;
        }

        public async Task<byte[]> TranslateTextToWav(string text)
        {
            var body = @"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
              <voice name='Microsoft Server Speech Text to Speech Voice (sv-SE, HedvigRUS)'>" +
                          text + "</voice></speak>";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(_host);
                    request.Content = new StringContent(body, Encoding.UTF8, "application/ssml+xml");
                    request.Headers.Add("Authorization", "Bearer " + AccessToken);
                    request.Headers.Add("Connection", "Keep-Alive");
                    request.Headers.Add("User-Agent", "YOUR_RESOURCE_NAME");
                    request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");

                    Debug.WriteLine("Calling the TTS service. Please wait... \n");
                    var response = await client.SendAsync(request);

                    var httpStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    //return httpStream.AsRandomAccessStream();

                    using (Stream stream = httpStream)
                    {
                        using (var ms = new MemoryStream())
                        {
                            byte[] waveBytes = null;
                            var count = 0;
                            do
                            {
                                var buf = new byte[1024];
                                count = stream.Read(buf, 0, 1024);
                                ms.Write(buf, 0, count);
                            } while (stream.CanRead && count > 0);

                            waveBytes = ms.ToArray();
                            return waveBytes;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<byte[]> TranslateTextToFile(string body)
        {
            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(_host);
                    request.Content = new StringContent(body, Encoding.UTF8, "application/ssml+xml");
                    request.Headers.Add("Authorization", "Bearer " + AccessToken);
                    request.Headers.Add("Connection", "Keep-Alive");
                    request.Headers.Add("User-Agent", "YOUR_RESOURCE_NAME");
                    request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");

                    Debug.WriteLine("Calling the TTS service. Please wait... \n");

                    Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

                    Windows.Storage.StorageFile sampleFile = await storageFolder.CreateFileAsync(DateTime.Now.ToString("yyyyMMdd") + "File.wav", Windows.Storage.CreationCollisionOption.OpenIfExists);

                    var fileStream = await sampleFile.OpenAsync(FileAccessMode.ReadWrite);

                    var response = await client.SendAsync(request);

                    Stream stream = await response.Content.ReadAsStreamAsync();
                    IInputStream inputStream = stream.AsInputStream();
                    ulong totalBytesRead = 0;
                    while (true)
                    {
                        // Read from the web.
                        IBuffer buffer = new Windows.Storage.Streams.Buffer(1024);
                        buffer = await inputStream.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.None);
                        if (buffer.Length == 0)
                        {
                            break;
                        }
                        totalBytesRead += buffer.Length;
                        await fileStream.WriteAsync(buffer);
                        Debug.WriteLine("TotalBytesRead: {0:f}", totalBytesRead);
                    }

                    inputStream.Dispose();

                    fileStream.Dispose();
                }
            }

            return null;
        }

        public async Task<string> GetAccessToken()
        {
            var auth = new Authentication("https://westeurope.api.cognitive.microsoft.com/sts/v1.0/issueToken",
                "c0a8bac399914fa380004d6033953a46");
            try
            {
                var accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                Debug.WriteLine("Successfully obtained an access token. \n");
                return accessToken;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to obtain an access token.");
                Debug.WriteLine(ex.ToString());
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
