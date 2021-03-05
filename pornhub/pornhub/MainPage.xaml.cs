using Xamarin.Essentials;
using Xamarin.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace pornhub
{

    static class Info
    {
        public static int IwaraPages
        {
            get => Preferences.Get(nameof(IwaraPages), 2);

            set => Preferences.Set(nameof(IwaraPages), value);
        }
    }

    public sealed class EventClicked
    {
        public Action Start { get; set; }

        public Action CopyPacUriTo { get; set; }

        public Action Open { get; set; }


    }

    public partial class MainPage : ContentPage
    {


        public MainPage(EventClicked eventClicked)
        {
            InitializeComponent();

            m_start.Clicked += (obj, e) => eventClicked.Start();

            m_copyUri.Clicked += (obj, e) => eventClicked.CopyPacUriTo();

            m_open.Clicked += (obj, e) => eventClicked.Open();



        }

        private void OnIwara(object sender, EventArgs e)
        {
            var info = new IwaraPageInfo(Info.IwaraPages);

            Navigation.PushModalAsync(new IwaraPage(info));


            info.Task.ContinueWith((t) => Info.IwaraPages = t.Result);
        }
    }
}