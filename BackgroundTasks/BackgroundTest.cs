using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Windows.ApplicationModel.Background;
using System.IO;
using System.Net.Http;
using Windows.Foundation.Collections;

namespace BackgroundTasks
{
    public sealed class BackgroundTest : IBackgroundTask
    {
        const string FriendlyName = "BackgroundTestName";

        public static BackgroundTaskRegistration Register()
        {
            //var status = BackgroundExecutionManager.RequestAccessAsync();

            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {

                if (cur.Value.Name == FriendlyName)
                {
                    // 
                    // The task is already registered.
                    // 
                    return (BackgroundTaskRegistration)(cur.Value);
                }
            }


            //http://msdn.microsoft.com/en-us/library/windows/apps/windows.applicationmodel.background.timetrigger.aspx
            IBackgroundTrigger trigger = new TimeTrigger(15, false);
            //
            // Builds the background task.
            //
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();

            builder.Name = FriendlyName;
            builder.TaskEntryPoint = typeof(BackgroundTest).FullName;
            builder.SetTrigger(trigger);

            //
            // Registers the background task, and get back a BackgroundTaskRegistration object representing the registered task.
            //
            BackgroundTaskRegistration task = builder.Register();
            return task;

        }

        const string fileName = "feed.xml";
        const string lastModifiedKey = "Last-Modified";

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            System.Diagnostics.Debug.WriteLine("Starting task");
            var deferral = taskInstance.GetDeferral();

            var folder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            HttpClient client = new HttpClient();

            if (localSettings.Values.ContainsKey(lastModifiedKey))
            {
                client.DefaultRequestHeaders.IfModifiedSince = (DateTimeOffset)localSettings.Values[lastModifiedKey];
            }

            try
            {
                var response = await client.GetAsync("http://jmservera.com/feed");
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    //nothing to update
                }
                else if (response.StatusCode == HttpStatusCode.OK)
                {
                    var file = await folder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (var fileStream = await file.OpenStreamForWriteAsync())
                        {
                            stream.CopyTo(fileStream);
                            fileStream.Flush();
                        }
                    }
                    //store the last modified value
                    localSettings.Values[lastModifiedKey] = response.Content.Headers.LastModified ?? null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("End Task");
                deferral.Complete();
            }
        }
    }
}