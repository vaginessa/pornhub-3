using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content;
using Xamarin.Essentials;
using Android.Content.Res;
using System.IO;
using System.Net;
using LeiKaiFeng.Pornhub;
using LeiKaiFeng.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using AndroidX.Core.App;
using System.Linq;

namespace pornhub.Droid
{


    public static class ServerInfo
    {
        public static byte[] AdPageCer { get; set; }

        public static byte[] AdVideo { get; set; }

        public static byte[] MainPageCer { get; set; }


        public static IPEndPoint PacServer { get; set; }

        public static IPEndPoint ProxyServer { get; set; }

        public static IPEndPoint ExportPacServer { get; set; }
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


    [Service]
    public sealed class ProxyServer : Service
    {
        const int ID = 345;

        public override void OnCreate()
        {
            base.OnCreate();

           
            var func = ServerHelper.CreateServerNotificationFunc("pornhub", this, MainActivity.CHANNEL_ID);

            var action = ServerHelper.CreateUpServerNotificationFunc(this, ID, func);

            StartForeground(ID, func("run"));


            Task.Run(async () =>
            {
                int n = 0;

                while (true)
                {
                    await Task.Delay(new TimeSpan(0, 0, 2)).ConfigureAwait(false);


                    action($"{n++}");
                }
            });

            StartProxy();
        }

        void StartProxy()
        {
            



            PacServer pacServer = PacServer.Start(ServerInfo.PacServer,
                PacServer.Create(ServerInfo.ProxyServer, "cn.pornhub.com", "hw-cdn2.adtng.com", "ht-cdn2.adtng.com", "vz-cdn2.adtng.com"),
                PacServer.Create(new IPEndPoint(IPAddress.Loopback, 80), "www.pornhub.com", "hubt.pornhub.com"));

            PornhubProxyInfo info = new PornhubProxyInfo
            {
                MainPageStreamCreate = Info.CreateLocalStream(new X509Certificate2(ServerInfo.MainPageCer)),

                ADPageStreamCreate = Info.CreateLocalStream(new X509Certificate2(ServerInfo.AdPageCer)),

                RemoteStreamCreate = Info.CreateRemoteStream,

                MaxContentSize = 1024 * 1024 * 5,

                ADVideoBytes = ServerInfo.AdVideo,

                CheckingVideoHtml = Info.CheckingVideoHtml,

                MaxRefreshRequestCount = 30,

                ReplaceResponseHtml = Info.ReplaceResponseHtml,


            };

            PornhubProxyServer server = new PornhubProxyServer(info);

            server.Start(ServerInfo.ProxyServer);
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }
    }


    public static class ServerHelper
    {
        public static void CreateNotificationChannel(ContextWrapper context, string channelID, string channelName)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                ((NotificationManager)context.GetSystemService(Context.NotificationService))
                            .CreateNotificationChannel(new NotificationChannel(channelID, channelName, NotificationImportance.Max) { LockscreenVisibility = NotificationVisibility.Public });
            }
        }


        public static void StartServer(ContextWrapper context, Intent intent)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }

        public static Action<string> CreateUpServerNotificationFunc(ContextWrapper context, int notificationID, Func<string, Notification> func)
        {
            return (contentText) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ((NotificationManager)context.GetSystemService(Context.NotificationService))
                            .Notify(notificationID, func(contentText));
                });



            };


        }


        public static Func<string, Notification> CreateServerNotificationFunc(string contentTitle, Context context, string channelID)
        {
            return (contentText) =>
            {
                return new NotificationCompat.Builder(context, channelID)
                               .SetContentTitle(contentTitle)
                               .SetContentText(contentText)
                               .SetSmallIcon(Resource.Mipmap.icon)
                               .SetOngoing(true)
                               .Build();
            };
        }
    }



    [Activity(Label = "pornhub", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize )]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        static byte[] ReadAllBytes(Func<Stream> func)
        {
            using (var stream = func())
            {

                MemoryStream memoryStream = new MemoryStream();

                stream.CopyTo(memoryStream);



                byte[] buffer = memoryStream.GetBuffer();

                Array.Resize(ref buffer, (int)memoryStream.Position);

                return buffer;
            }
        }

        void InitServerInfo()
        {
            var assets = this.Assets;

            ServerInfo.AdVideo = MainActivity.ReadAllBytes(() => assets.Open("ad_video.mp4"));

            ServerInfo.AdPageCer = MainActivity.ReadAllBytes(() => assets.Open("ad.com.pfx"));

            ServerInfo.MainPageCer = MainActivity.ReadAllBytes(() => assets.Open("main.com.pfx"));


            var ip = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault() ?? IPAddress.Loopback;

            ServerInfo.PacServer = new IPEndPoint(IPAddress.Any, 59237);

            ServerInfo.ExportPacServer = new IPEndPoint(ip, ServerInfo.PacServer.Port);

            ServerInfo.ProxyServer = new IPEndPoint(ip, 43433);
        }

        EventClicked CreateEventClicked()
        {
            return new EventClicked
            {
                CopyPacUriTo = () =>
                {
                    
                    
                    Xamarin.Essentials.Clipboard.SetTextAsync(PacServer.CreatePacUri(ServerInfo.ExportPacServer).AbsoluteUri);
                },

                Start = () =>
                {
                    Intent intent = new Intent(this, typeof(ProxyServer));

                    ServerHelper.StartServer(this, intent);
                },

                Open = () =>
                {

                    Browser.OpenAsync("https://cn.pornhub.com/", BrowserLaunchMode.External);
                },

            };
        }

        public const string CHANNEL_ID = "fdfserte54354";

        public const string CHANNEL_NAME = "PROXY";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);
            


            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);


            InitServerInfo();

            ServerHelper.CreateNotificationChannel(this, CHANNEL_ID, CHANNEL_NAME);

            LoadApplication(new App(CreateEventClicked()));    
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}