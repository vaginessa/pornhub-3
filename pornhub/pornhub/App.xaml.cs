using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace pornhub
{
    public partial class App : Application
    {
        public App(EventClicked eventClicked)
        {
            InitializeComponent();

            MainPage = new MainPage(eventClicked);
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
