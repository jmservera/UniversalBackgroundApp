namespace BackgroundTasks
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Foundation;
    using Windows.Storage;
    using Windows.Web.Http;
    using AppData = Windows.Storage.ApplicationData;
    public sealed class CachedFileController
    {
        const string fileName = "feed.xml";
        const string lastModifiedKey = "Last-Modified";
        const string feedUrl = "http://jmservera.com/feed/";

        public event EventHandler<string> NotifyMessage;

        private void OnNotifyMessage(string message)
        {
            if (NotifyMessage != null)
                NotifyMessage(this, message);
        }

        public IAsyncOperationWithProgress<string, HttpProgress> GetFileAsync()
        {
            return AsyncInfo.Run<string, HttpProgress>(
                (cancellationToken, progress) => getFileAsync(cancellationToken, progress));
        }

        public IAsyncActionWithProgress<HttpProgress> DownloadFileAsync()
        {
            return AsyncInfo.Run<HttpProgress>(
                (cancellationToken, progress) => downloadFileAsync(cancellationToken, progress));
        }

        private async Task<string> getFileAsync(CancellationToken cancellationToken, IProgress<HttpProgress> progress)
        {
            var folder = AppData.Current.TemporaryFolder;
            StorageFile file = null;
            try
            {
                file = await folder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
            }
            if (file == null)
            {
                await downloadFileAsync(cancellationToken, progress);
                try
                {
                    file = await folder.GetFileAsync(fileName);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                using (var fileStream = await file.OpenSequentialReadAsync())
                {
                    using (StreamReader r = new StreamReader(fileStream.AsStreamForRead()))
                    {
                        return await r.ReadToEndAsync();
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1,1);

        private async Task downloadFileAsync(CancellationToken cancellationToken, IProgress<HttpProgress> progress)
        {
            OnNotifyMessage("activity");
            System.Diagnostics.Debug.WriteLine("Starting task");

            var folder = AppData.Current.TemporaryFolder;
            var localSettings = AppData.Current.LocalSettings;

            HttpClient client = new HttpClient();

            if (localSettings.Values.ContainsKey(lastModifiedKey))
            {
                //this will avoid downloading a large file when we already have a fresh copy
                client.DefaultRequestHeaders.IfModifiedSince = (DateTimeOffset)localSettings.Values[lastModifiedKey];
                //maybe we could also use ETag...
            }

            try
            {
                var response=await client.GetAsync(new Uri(feedUrl)).AsTask(cancellationToken,progress);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    //nothing to update, we already have the good one
                    OnNotifyMessage("alert");
                }
                else if (response.StatusCode == HttpStatusCode.Ok)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (var istream = await response.Content.ReadAsInputStreamAsync())
                    {
                        var stream = istream.AsStreamForRead();
                        cancellationToken.ThrowIfCancellationRequested();
                        await _semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            var file = await folder.CreateFileAsync(fileName,
                                Windows.Storage.CreationCollisionOption.ReplaceExisting);
                            using (var fileStream = await file.OpenStreamForWriteAsync())
                            {
                                await stream.CopyToAsync(fileStream, 4096, cancellationToken);
                            }
                            //store the last modified value, cannot store it inside the file properties, not allowed by w8
                            localSettings.Values[lastModifiedKey] = response.Content.Headers.LastModified ?? null;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }

                    //show badge notification
                    OnNotifyMessage("attention");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Request returned error {0}:{1}", (int)response.StatusCode,
                        response.StatusCode);
                    OnNotifyMessage("error");
                }
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("End Task");
            }
        }

        public async void ClearCacheAsync()
        {
            var folder = AppData.Current.TemporaryFolder;
            var localSettings = AppData.Current.LocalSettings;

            StorageFile file = null;
            try
            {
                file = await folder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
            }
            if (file != null)
            {
                _semaphore.Wait();
                try
                {
                    try
                    {
                        //double check for thread safety
                        file = await folder.GetFileAsync(fileName);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                    if (file != null)
                    {
                        await file.DeleteAsync();
                    }
                    localSettings.Values[lastModifiedKey] = null;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
    }
}