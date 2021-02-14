using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace pornhub
{
    public partial class App : Application
    {
        public App(Source source)
        {
            InitializeComponent();

            MainPage = new MainPage(source);
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
