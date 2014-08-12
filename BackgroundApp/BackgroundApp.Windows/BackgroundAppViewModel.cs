using BackgroundTasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Web.Syndication;

namespace BackgroundApp
{
    public class BackgroundAppViewModel:INotifyPropertyChanged
    {
        BackgroundTest test = new BackgroundTest();

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


        public BackgroundAppViewModel()
        {
            Load();
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
