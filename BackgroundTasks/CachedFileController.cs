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

        private static AsyncSemaphore _semaphore = new AsyncSemaphore(1, 1, "BackgroundTasks.CachedFileController");

        public event EventHandler<string> NotifyMessage;

        private void OnNotifyMessage(string message)
        {
            if (NotifyMessage != null)
                NotifyMessage(this, message);
        }

        public IAsyncOperationWithProgress<string, HttpProgress> GetFileAsync()
        {
            return AsyncInfo.Run<string, HttpProgress>(
                async (cancellationToken, progress) =>
                {
                    var folder = AppData.Current.TemporaryFolder;
                    StorageFile file = null;

                    try
                    {
                        file = await folder.GetFileAsync(fileName);
                    }
                    catch (FileNotFoundException)
                    {
                        Logger.Log("file not found, need to download");
                    }
                    if (file == null)
                    {
                        await DownloadFileAsync();
                        try
                        {
                            file = await folder.GetFileAsync(fileName);
                        }
                        catch (FileNotFoundException)
                        {
                            Logger.Log("file not found, something bizarre happened while downoladiong :(");
                            return null;
                        }
                    }
                    Logger.Log("getFile semaphore");
                    await _semaphore.WaitOneAsync();
                    try
                    {
                        Logger.Log("getFile semaphore taken");
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
                        Logger.Log("getFile semaphore released");
                    }
                });
        }

        public IAsyncActionWithProgress<HttpProgress> DownloadFileAsync()
        {
            return AsyncInfo.Run<HttpProgress>(
                async (cancellationToken, progress) =>
                {
                    OnNotifyMessage("activity");

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
                            using (var contentStream = await response.Content.ReadAsInputStreamAsync())
                            {
                                var stream = contentStream.AsStreamForRead();
                                Logger.Log("downloadfile semaphore");
                                await _semaphore.WaitOneAsync();
                                try
                                {
                                    Logger.Log("downloadfile semaphore taken");
                                    var file = await folder.CreateFileAsync(fileName,
                                        Windows.Storage.CreationCollisionOption.ReplaceExisting);
                                    using (var fileStream = await file.OpenStreamForWriteAsync())
                                    {
                                        await stream.CopyToAsync(fileStream, 4096, cancellationToken);
                                    }
                                    //store the last modified value, cannot store it inside the file properties, not allowed by w8
                                    localSettings.Values[lastModifiedKey] = response.Content.Headers.LastModified ?? null;
                                    //show badge notification
                                    OnNotifyMessage("newMessage");
                                }
                                finally
                                {
                                    _semaphore.Release();
                                    Logger.Log("downloadfile semaphore released");
                                }
                            }
                        }
                        else
                        {
                            Logger.Log("Request returned error {0}:{1}",
                                (int)response.StatusCode, response.StatusCode);
                            OnNotifyMessage("error");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Request threw exception {0}:{1}", ex.HResult, ex.Message);
                        OnNotifyMessage("error");
                    }
                });
        }

        public async void ClearCacheAsync()
        {
            var folder = AppData.Current.TemporaryFolder;
            var localSettings = AppData.Current.LocalSettings;

            StorageFile file = null;
            Logger.Log("clearcache semaphore");
            await _semaphore.WaitOneAsync();
            try
            {
                Logger.Log("clearcache semaphore taken");
                try
                {
                    file = await folder.GetFileAsync(fileName);
                }
                catch (FileNotFoundException)
                {
                    Logger.Log("file not found, will not delete it :)");
                }

                if (file != null)
                {
                    await file.DeleteAsync();
                }
                localSettings.Values[lastModifiedKey] = null;
            }
            catch (FileNotFoundException)
            {
                Logger.Log("file not found while deleting, will not delete it :)");
            }
            finally
            {
                _semaphore.Release();
                Logger.Log("clearcache semaphore released");
            }
        }
    }
}