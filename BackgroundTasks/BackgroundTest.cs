namespace BackgroundTasks
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.Background;
    using Windows.Data.Xml.Dom;
    using Windows.Foundation;
    using Windows.Storage;
    using Notifications = Windows.UI.Notifications;
    public sealed class BackgroundTest : IBackgroundTask
    {
        const string FriendlyName = "BackgroundTestName";

        static BackgroundTaskRegistration _task;

        public static IAsyncOperation<BackgroundTaskRegistration> RegisterTaskAsync()
        {
            return registerAsyncCore().AsAsyncOperation();
        }

        public static bool IsTaskRegistered()
        {
            if (_task == null)
            {
                foreach (var cur in BackgroundTaskRegistration.AllTasks)
                {
                    if (cur.Value.Name == FriendlyName)
                    {
                        // The task is already registered.
                        _task = (BackgroundTaskRegistration)(cur.Value);
                    }
                }
            }
            return _task != null;
        }
        private static async Task<BackgroundTaskRegistration> registerAsyncCore()
        {
            if (IsTaskRegistered())
                return _task;

            await BackgroundExecutionManager.RequestAccessAsync();

            //http://msdn.microsoft.com/en-us/library/windows/apps/windows.applicationmodel.background.timetrigger.aspx
            IBackgroundTrigger trigger = new TimeTrigger(360, false); //6 hours

            // Builds the background task.
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();

            builder.Name = FriendlyName;
            builder.TaskEntryPoint = typeof(BackgroundTest).FullName;
            builder.SetTrigger(trigger);

            // Registers the background task, and get back a BackgroundTaskRegistration object representing the registered task.
            return builder.Register();
        }

        public static void UnregisterTask()
        {
            if (IsTaskRegistered())
            {
                _task.Unregister(true);
            }
        }

        const string fileName = "feed.xml";
        const string lastModifiedKey = "Last-Modified";
        const string feedUrl = "http://jmservera.com/feed";

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            //it is an async work that could take long, we need to get a deferral...
            var deferral = taskInstance.GetDeferral();
            try
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                taskInstance.Canceled += (o, e) => tokenSource.Cancel();
                await downloadFile(tokenSource.Token);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private static async Task downloadFile(CancellationToken cancellationToken)
        {
            ShowNotification("activity");
            System.Diagnostics.Debug.WriteLine("Starting task");

            var folder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            HttpClient client = new HttpClient();

            if (localSettings.Values.ContainsKey(lastModifiedKey))
            {
                //this will avoid downloading a large file when we already have a fresh copy
                client.DefaultRequestHeaders.IfModifiedSince = (DateTimeOffset)localSettings.Values[lastModifiedKey];
                //maybe we could also use ETag...
            }

            try
            {
                var response = await client.GetAsync(feedUrl, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    //nothing to update, we already have the good one
                    ShowNotification("alert");
                }                 
                else if (response.StatusCode == HttpStatusCode.OK)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var file = await folder.CreateFileAsync(fileName, 
                        Windows.Storage.CreationCollisionOption.ReplaceExisting);

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using (var fileStream = await file.OpenStreamForWriteAsync())
                        {
                           await stream.CopyToAsync(fileStream,4096,cancellationToken);
                        }
                    }

                    //store the last modified value, cannot store it inside the file properties, not allowed by w8
                    localSettings.Values[lastModifiedKey] = response.Content.Headers.LastModified ?? null;

                    //show badge notification
                    ShowNotification("attention");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Request returned error {0}:{1}", (int)response.StatusCode,
                        response.StatusCode);
                    ShowNotification("error");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Download File Exception: {0}",ex.Message);
                ShowNotification("error");
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("End Task");
            }
        }

        public IAsyncOperation<string> GetFile()
        {
            return getFile().AsAsyncOperation();
        }

        private async Task<string> getFile()
        {
            var folder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
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
                await downloadFile(new CancellationToken());
                try
                {
                    file = await folder.GetFileAsync(fileName);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }
            using (var fileStream = await file.OpenSequentialReadAsync())
            {
                using (StreamReader r = new StreamReader(fileStream.AsStreamForRead()))
                {
                    return await r.ReadToEndAsync();
                }
            }
        }
        public static void ShowNotification(string value) //"alert" or "activity"
        {
            if (!string.IsNullOrEmpty(value))
            {
                var badgeXml = Notifications.BadgeUpdateManager.GetTemplateContent(Notifications.BadgeTemplateType.BadgeGlyph);
                var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                badgeElement.SetAttribute("value", value);
                var notification = new Notifications.BadgeNotification(badgeXml);
                Notifications.BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(notification);
            }
            else
            {
                Notifications.BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
            }
        }

        public static void ClearNotification()
        {
            ShowNotification(null);
        }

        
    }
}