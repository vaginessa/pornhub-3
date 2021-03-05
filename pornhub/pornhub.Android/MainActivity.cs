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
using System.Security.Authentication;
using static Android.OS.PowerManager;
using static Xamarin.Essentials.Permissions;

[assembly:MetaData("android.webkit.WebView.EnableSafeBrowsing", Value = "false")]

namespace pornhub.Droid
{

    static class PornhubHelper
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



    }

    public static class ServerInfo
    {
        
        public static byte[] CaCert { get; set; }

        public static byte[] AdVideo { get; set; }

        public static byte[] AdCert { get; set; }

        public static byte[] PornCert { get; set; }

        public static IPEndPoint PacServerEndPoint { get; set; }

        public static IPEndPoint PornhubProxyEndPoint { get; set; }

        public static IPEndPoint ADErrorEndPoint { get; set; }

        public static IPEndPoint IwaraProxyEndPoint { get; set; }

        public static IPEndPoint ExportPacServerEndPoint { get; set; }


    }


    [Service]
    public sealed class ProxyServer : Service
    {
        const int ID = 345;

        WakeLock m_WakeLock;

        public override void OnCreate()
        {
            base.OnCreate();

           
            var func = ServerHelper.CreateServerNotificationFunc("pornhub", this, MainActivity.CHANNEL_ID);

            var action = ServerHelper.CreateUpServerNotificationFunc(this, ID, func);

            StartForeground(ID, func("run"));

            PowerManager powerManager = (PowerManager)GetSystemService(Context.PowerService);
            m_WakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial,
                    "MyApp::MyWakelockTag");
            m_WakeLock.Acquire();

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





            const string PORNHUB_HOST = "www.livehub.com";

            const string IWARA_HOST = "iwara.tv";


            var pornhubListensEndPoint = ServerInfo.PornhubProxyEndPoint;
            var pacListensEndPoint = ServerInfo.PacServerEndPoint;
            var adErrorEndpoint = ServerInfo.ADErrorEndPoint;
            var iwaraLsitensPoint = ServerInfo.IwaraProxyEndPoint;
            var adVido = ServerInfo.AdVideo;
            var caFile = ServerInfo.CaCert;






            var mainCert = new X509Certificate2(ServerInfo.PornCert);
            var adCert = new X509Certificate2(ServerInfo.AdCert);
           

            PacServer.Builder.Create(pacListensEndPoint)
                .Add((host) => host == "www.pornhub.com", ProxyMode.CreateHTTP(adErrorEndpoint))
                .Add((host) => host == "hubt.pornhub.com", ProxyMode.CreateHTTP(adErrorEndpoint))
                .Add((host) => host == "ajax.googleapis.com", ProxyMode.CreateHTTP(adErrorEndpoint))
                .Add((host) => PacMethod.dnsDomainIs(host, "pornhub.com"), ProxyMode.CreateHTTP(pornhubListensEndPoint))
                .Add((host) => PacMethod.dnsDomainIs(host, "adtng.com"), ProxyMode.CreateHTTP(pornhubListensEndPoint))
                .Add((host) => host == "i.iwara.tv", ProxyMode.CreateDIRECT())
                .Add((host) => PacMethod.dnsDomainIs(host, IWARA_HOST), ProxyMode.CreateHTTP(iwaraLsitensPoint))
                .StartPACServer();

            PornhubProxyInfo info = new PornhubProxyInfo
            {
                MainPageStreamCreate = ConnectHelper.CreateLocalStream(new X509Certificate2(mainCert), SslProtocols.Tls12),

                ADPageStreamCreate = ConnectHelper.CreateLocalStream(new X509Certificate2(adCert), SslProtocols.Tls12),

                RemoteStreamCreate = ConnectHelper.CreateRemoteStream(PORNHUB_HOST, 443, PORNHUB_HOST, (a, b) => new MHttpStream(a, b), SslProtocols.Tls12),

                MaxContentSize = 1024 * 1024 * 5,

                ADVideoBytes = adVido,

                CheckingVideoHtml = PornhubHelper.CheckingVideoHtml,

                MaxRefreshRequestCount = 30,

                ReplaceResponseHtml = PornhubHelper.ReplaceResponseHtml,

                ListenIPEndPoint = pornhubListensEndPoint
            };


            Task t1 = PornhubProxyServer.Start(info).Task;

            TunnelProxyInfo iwaraSniInfo = new TunnelProxyInfo()
            {
                ListenIPEndPoint = iwaraLsitensPoint,
                CreateLocalStream = ConnectHelper.CreateDnsLocalStream(),
                CreateRemoteStream = ConnectHelper.CreateDnsRemoteStream(
                    443,
                    "104.26.12.12",
                    "104.20.201.232",
                    "104.24.48.227",
                    "104.22.27.126",
                    "104.24.53.193")
            };

            Task t2 = TunnelProxy.Start(iwaraSniInfo).Task;


        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }


        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            m_WakeLock.Release();

            base.OnDestroy();
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


            ServerInfo.PornCert = ReadAllBytes(() => assets.Open("main.com.pfx"));

            ServerInfo.AdCert = ReadAllBytes(() => assets.Open("ad.com.pfx"));

            ServerInfo.CaCert = ReadAllBytes(() => assets.Open("myCA.pfx"));

            var ip = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault() ?? IPAddress.Loopback;

            ServerInfo.PacServerEndPoint = new IPEndPoint(IPAddress.Any, 59237);

            ServerInfo.ExportPacServerEndPoint = new IPEndPoint(ip, ServerInfo.PacServerEndPoint.Port);

            ServerInfo.PornhubProxyEndPoint = new IPEndPoint(ip, 43433);

            ServerInfo.ADErrorEndPoint = new IPEndPoint(IPAddress.Loopback, 80);

            ServerInfo.IwaraProxyEndPoint = new IPEndPoint(ip, 43455);
        }

        EventClicked CreateEventClicked()
        {
            return new EventClicked
            {
                CopyPacUriTo = () =>
                {
                    
                    
                    Xamarin.Essentials.Clipboard.SetTextAsync(PacServer.CreatePacUri(ServerInfo.ExportPacServerEndPoint).AbsoluteUri);
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

            Xamarin.Essentials.Permissions.RequestAsync<StorageWrite>();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;


            AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironment_UnhandledExceptionRaiser;

            InitServerInfo();

            ServerHelper.CreateNotificationChannel(this, CHANNEL_ID, CHANNEL_NAME);

            LoadApplication(new App(CreateEventClicked()));    
        }


        static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log("TaskScheduler", e.Exception);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log("Domain", e.ExceptionObject);
        }

        static void AndroidEnvironment_UnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            Log("Android", e.Exception);
        }

        static void Log(string name, object e)
        {
            string s = System.Environment.NewLine;

            File.AppendAllText($"/storage/emulated/0/pornhub.{name}.txt", $"{s}{s}{s}{s}{DateTime.Now}{s}{e}", System.Text.Encoding.UTF8);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}