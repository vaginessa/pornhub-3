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
using LeiKaiFeng.Http;
using LeiKaiFeng.Pornhub;
using System.Linq;

namespace pornhub
{

    public sealed class Source
    {
        //public byte[] MainPageCer { get; set; }

        public byte[] AdPageCer { get; set; }

        public byte[] AdVideo { get; set; }

        public byte[] MainPageCer { get; set; }

        public byte[] CaCer { get; set; }

        public string RootPath { get; set; }
    }


    static class Info
    {
        public static string ReplaceResponseHtml(string html)
        {
            return html;

            //return new StringBuilder(html)
            //    .Replace("ci.", "ei.")
            //    .Replace("di.", "ei.")
            //    .ToString();
        }

        public static bool CheckingVideoHtml(string html)
        {
            if (html.Contains("/ev-h.p") ||
                html.Contains("/ev.p"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task<MHttpStream> CreateRemoteStream()
        {
            const string HOST = "www.livehub.com";

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(HOST, 443).ConfigureAwait(false);

            SslStream sslStream = new SslStream(new NetworkStream(socket, true), false, (a, b, c, d) => true);


            await sslStream.AuthenticateAsClientAsync(HOST, null, System.Security.Authentication.SslProtocols.Tls12, false).ConfigureAwait(false);

            return new MHttpStream(socket, sslStream);
        }




        public static Func<Stream, string, Task<Stream>> CreateLocalStream(X509Certificate certificate)
        {
            return async (stream, host) =>
            {

                SslStream sslStream = new SslStream(stream, false);

                await sslStream.AuthenticateAsServerAsync(certificate).ConfigureAwait(false);


                return sslStream;
            };

        }
    }

    public partial class MainPage : ContentPage
    {


        public MainPage(Source source)
        {
            InitializeComponent();

            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 43433);

            PacServer pacServer = PacServer.Start(new IPEndPoint(IPAddress.Loopback, 59237),
                PacServer.Create(endPoint, "cn.pornhub.com", "hw-cdn2.adtng.com", "ht-cdn2.adtng.com", "vz-cdn2.adtng.com"),
                PacServer.Create(new IPEndPoint(IPAddress.Loopback, 80), "www.pornhub.com", "hubt.pornhub.com"));

            PornhubProxyInfo info = new PornhubProxyInfo
            {
                MainPageStreamCreate = Info.CreateLocalStream(new X509Certificate2(source.MainPageCer)),

                ADPageStreamCreate = Info.CreateLocalStream(new X509Certificate2(source.AdPageCer)),

                RemoteStreamCreate = Info.CreateRemoteStream,

                MaxContentSize = 1024 * 1024 * 5,

                ADVideoBytes = source.AdVideo,

                CheckingVideoHtml = Info.CheckingVideoHtml,

                MaxRefreshRequestCount = 30,

                ReplaceResponseHtml = Info.ReplaceResponseHtml,


            };

            PornhubProxyServer server = new PornhubProxyServer(info);


            server.Start(endPoint);

            m_copyCaCer.Clicked += (obj, e) =>
            {
                Permissions.RequestAsync<Permissions.StorageWrite>()
                .ContinueWith((task) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (task.Result == PermissionStatus.Granted)
                        {

                            File.WriteAllBytes(Path.Combine(source.RootPath, "pornhubCa.crt"), source.CaCer);
                        }
                        else
                        {
                            DisplayAlert("消息", "没有存储权限导出失败", "确定");
                        }
                    });
                });

            };

            m_copyUri.Clicked += (obj, e) =>
            {
                Xamarin.Essentials.Clipboard.SetTextAsync(pacServer.ProxyUri.AbsoluteUri);
            };
        }

        void OnOpenBrowser(object sender, EventArgs e)
        {
            Browser.OpenAsync("https://cn.pornhub.com/", BrowserLaunchMode.SystemPreferred);
        }
    }
}