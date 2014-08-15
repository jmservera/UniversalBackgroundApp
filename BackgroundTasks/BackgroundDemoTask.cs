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
    using Windows.Web.Http;
    using Notifications = Windows.UI.Notifications;
    public sealed class BackgroundDemoTask : IBackgroundTask
    {
        const string FriendlyName = "BackgroundDemoTask";

        static BackgroundTaskRegistration _current;
        public static BackgroundTaskRegistration Current
        {
            get
            {
                IsTaskRegistered();
                return _current;
            }
        }

        public static IAsyncOperation<BackgroundTaskRegistration> RegisterTaskAsync()
        {
            return registerAsyncCore().AsAsyncOperation();
        }

        public static bool IsTaskRegistered()
        {
            if (_current == null)
            {
                foreach (var cur in BackgroundTaskRegistration.AllTasks)
                {
                    if (cur.Value.Name == FriendlyName)
                    {
                        // The task is already registered.
                        _current = (BackgroundTaskRegistration)(cur.Value);
                    }
                }
            }
            return _current != null;
        }
        private static async Task<BackgroundTaskRegistration> registerAsyncCore()
        {
            if (IsTaskRegistered())
                return _current;

            await BackgroundExecutionManager.RequestAccessAsync();

            //http://msdn.microsoft.com/en-us/library/windows/apps/windows.applicationmodel.background.timetrigger.aspx
            IBackgroundTrigger trigger = new TimeTrigger(15, false); //6 hours

            // Builds the background task.
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();

            builder.Name = FriendlyName;
            builder.TaskEntryPoint = typeof(BackgroundDemoTask).FullName;
            builder.SetTrigger(trigger);

            SystemCondition condition = new SystemCondition(SystemConditionType.InternetAvailable);
            builder.AddCondition(condition);

            // Registers the background task, and get back a BackgroundTaskRegistration object representing the registered task.
            _current = builder.Register();
            return _current;
        }

        public static void UnregisterTask()
        {
            if (IsTaskRegistered())
            {
                _current.Unregister(true);
                _current = null;
            }
        }

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            //it is an async work that could take long, we need to get a deferral...
            var deferral = taskInstance.GetDeferral();
            try
            {
                CachedFileController fileController = new CachedFileController();

                fileController.NotifyMessage += (o,message) =>
                {
                    ShowNotificationBadge(message);
                };
                var asyncDownload= fileController.DownloadFileAsync();
                asyncDownload.Progress = (o, p) =>
                {
                    taskInstance.Progress = (uint)(p.BytesReceived * 100 / (p.TotalBytesToReceive ?? (50 * 1024)));
                };
                await asyncDownload;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Download File Exception: {0}", ex.Message);
                ShowNotificationBadge("error");
            }
            finally
            {
                deferral.Complete();
            }
        }

        /// <summary>
        /// Shows badge notifications in live tile
        /// </summary>
        /// <param name="value">"alert", "activity" or String.Empty/null to clear</param>
        public static void ShowNotificationBadge(string value) 
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
            ShowNotificationBadge(null);
        }
    }
}