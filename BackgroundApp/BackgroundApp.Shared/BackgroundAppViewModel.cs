namespace BackgroundApp
{
    using BackgroundTasks;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using Windows.Data.Xml.Dom;
    using Windows.Web.Syndication;
    public class BackgroundAppViewModel:INotifyPropertyChanged
    {
        BackgroundDemoTask test = new BackgroundDemoTask();

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
                    BackgroundDemoTask.UnregisterTask();
                    _isTaskRegistered = false;
                }
                else
                {
                    await BackgroundDemoTask.RegisterTaskAsync();
                    _isTaskRegistered = BackgroundDemoTask.IsTaskRegistered();
                    OnPropertyChanged("IsTaskRegistered");
                }
            }
        }

        public BackgroundAppViewModel()
        {
            Load();
            
            _isTaskRegistered = BackgroundDemoTask.IsTaskRegistered();
        }

        public async void Load()
        {
            IsLoading = true;
            try
            {
                var value = await test.GetFile();
                XmlDocument doc=new XmlDocument();
                SyndicationFeed feed = new SyndicationFeed();
                doc.LoadXml(value);
                feed.LoadFromXml(doc);
                Feed = feed;
            }
            finally
            {
                IsLoading = false;
            }
        }

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
