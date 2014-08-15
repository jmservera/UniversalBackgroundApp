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
            while (!_semaphore.WaitOne(0))
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
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

        private static Semaphore _semaphore;
        static CachedFileController()
        {
            if (!Semaphore.TryOpenExisting("BackgroundTasks.CachedFileController", out _semaphore))
                _semaphore = new Semaphore(1, 1, "BackgroundTasks.CachedFileController");
        }
        public IAsyncActionWithProgress<HttpProgress> DownloadFileAsync()
        {
            return AsyncInfo.Run<HttpProgress>(
                (cancellationToken, progress) => downloadFileAsync(cancellationToken, progress));
        }
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
                var response = await client.GetAsync(new Uri(feedUrl)).AsTask(cancellationToken, progress);
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
                        while (!_semaphore.WaitOne(0))
                        {
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Request thrown exception {0}:{1}", 
                    ex.HResult,
                    ex.Message);
                OnNotifyMessage("error");
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
            while (!_semaphore.WaitOne(0))
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
            try
            {
                try
                {
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
            catch (FileNotFoundException)
            {
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}