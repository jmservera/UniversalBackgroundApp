namespace BackgroundApp
{
    using BackgroundTasks;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Data.Xml.Dom;
using Windows.UI.Core;
using Windows.Web.Syndication;
    public class BackgroundAppViewModel:INotifyPropertyChanged
    {
        CachedFileController fileController = new CachedFileController();

        private bool _isLoading;

        public bool IsLoading
        {
            get { return _isLoading; }
            set { SetProperty(ref _isLoading , value); }
        }

        private SyndicationFeed _feed;

        public SyndicationFeed Feed
        {
            get { return _feed; }
            set { SetProperty(ref _feed , value); }
        }

        private bool _isTaskRegistered;

        public bool IsTaskRegistered
        {
            get { return _isTaskRegistered; }
            set
            {
                setRegisteredTask(value);
            }
        }

        private async void setRegisteredTask(bool register)
        {
            if (_isTaskRegistered != register)
            {
                if (_isTaskRegistered)
                {
                    dettachTask();
                    BackgroundDemoTask.UnregisterTask();
                    _isTaskRegistered = false;
                }
                else
                {
                    await BackgroundDemoTask.RegisterTaskAsync();
                    attachTask();
                    _isTaskRegistered = BackgroundDemoTask.IsTaskRegistered();
                    OnPropertyChanged("IsTaskRegistered");
                }
            }
        }
        CoreDispatcher dispatcher;
        public BackgroundAppViewModel()
        {
            ClearFileCommand = new RelayCommand(() =>
            {
                fileController.ClearCacheAsync();
                Clear();
            },() => { return true; });

            Load();
            _isTaskRegistered = BackgroundDemoTask.IsTaskRegistered();
            attachTask();
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }
            this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        }

        private bool _taskCompleted;

        public bool TaskCompleted
        {
            get { return _taskCompleted; }
            set
            {
                if (!_taskCompleted && value)
                {
                    Load();
                }
                SetProperty(ref _taskCompleted, value);
            }
        }

        private uint _taskProgress;
        public uint TaskProgress
        {
            get { return _taskProgress; }
            set
            {
                SetProperty(ref _taskProgress, value);
                if (value == 0)
                {
                    Clear();
                }
            }
        }

        private void Clear()
        {
            Feed = null;
        }

        private void attachTask()
        {
            var task = BackgroundDemoTask.Current;
            if (task != null)
            {
                task.Completed += task_Completed;
                task.Progress += task_Progress;
            }
        }
        private void dettachTask()
        {
            var task = BackgroundDemoTask.Current;
            if (task != null)
            {
                task.Completed -= task_Completed;
                task.Progress -= task_Progress;
            }
        }

        async void task_Progress(Windows.ApplicationModel.Background.BackgroundTaskRegistration sender, Windows.ApplicationModel.Background.BackgroundTaskProgressEventArgs args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TaskCompleted = false;
                TaskProgress = args.Progress;
            });
        }

        async void task_Completed(Windows.ApplicationModel.Background.BackgroundTaskRegistration sender, Windows.ApplicationModel.Background.BackgroundTaskCompletedEventArgs args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TaskCompleted = true;
            });
        }


        public async void Load()
        {
            IsLoading = true;
            try
            {
                var value = await fileController.GetFileAsync();
                XmlDocument doc=new XmlDocument();
                SyndicationFeed feed = new SyndicationFeed();
                doc.LoadXml(value);
                feed.LoadFromXml(doc);
                Feed = feed;
            }
            catch (Exception ex)
            {
                Feed = new SyndicationFeed(ex.Message, ex.Message, null);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public ICommand ClearFileCommand { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] String propertyName = null)
        {
            if (object.Equals(storage, value)) return false;
            storage = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
