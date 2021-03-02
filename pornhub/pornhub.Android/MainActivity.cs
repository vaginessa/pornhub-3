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

namespace pornhub.Droid
{

    public sealed class SniProxyInfo
    {
        public SniProxyInfo(IPEndPoint iPEndPoint, Func<Stream, string, Task<Stream>> createLocalStream, Func<Task<Stream>> createRemoteStream)
        {
            IPEndPoint = iPEndPoint;
            CreateLocalStream = createLocalStream;
            CreateRemoteStream = createRemoteStream;
        }

        public IPEndPoint IPEndPoint { get; }


        public Func<Stream, string, Task<Stream>> CreateLocalStream { get; }

        public Func<Task<Stream>> CreateRemoteStream { get; }

    }

    public sealed class SniProxy
    {
        SniProxyInfo m_info;


        public SniProxy(SniProxyInfo info)
        {
            m_info = info;
        }


        static async Task CatchAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {

            }
        }

        async Task Connect(Stream left_stream)
        {
            Stream right_stream;

            left_stream = await LeiKaiFeng.Proxys.ConnectHelper.ReadConnectRequestAsync(left_stream, m_info.CreateLocalStream).Unwrap().ConfigureAwait(false);

            right_stream = await m_info.CreateRemoteStream().ConfigureAwait(false);



            var t1 = left_stream.CopyToAsync(right_stream, 2048);

            var t2 = right_stream.CopyToAsync(left_stream);

            await Task.WhenAny(t1, t2).ConfigureAwait(false);


            left_stream.Close();

            right_stream.Close();

            CatchAsync(t1);

            CatchAsync(t2);
        }

        public Task Start()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);


            socket.Bind(m_info.IPEndPoint);

            socket.Listen(6);


            return Task.Run(async () =>
            {
                while (true)
                {
                    var connent = await socket.AcceptAsync().ConfigureAwait(false);

                    Task task = Task.Run(() => Connect(new NetworkStream(connent, true)));
                }
            });
        }



    }




    static class Connect
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


        public static Func<Task<T>> CreateRemoteStream<T>(string host, int port, string sni, Func<Socket, SslStream, T> func, SslProtocols sslProtocols = SslProtocols.None)
        {
            return async () =>
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                await socket.ConnectAsync(host, port).ConfigureAwait(false);

                SslStream sslStream = new SslStream(new NetworkStream(socket, true), false);


                var info = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (a, b, c, d) => true,

                    EnabledSslProtocols = sslProtocols,

                    TargetHost = sni
                };

                await sslStream.AuthenticateAsClientAsync(info, default).ConfigureAwait(false);

                return func(socket, sslStream);
            };



        }

        public static Func<Stream, string, Task<Stream>> CreateLocalStream(X509Certificate certificate, SslProtocols sslProtocols = SslProtocols.None)
        {
            return async (stream, host) =>
            {
                SslStream sslStream = new SslStream(stream, false);

                var info = new SslServerAuthenticationOptions()
                {
                    ServerCertificate = certificate,

                    EnabledSslProtocols = sslProtocols
                };

                await sslStream.AuthenticateAsServerAsync(info, default).ConfigureAwait(false);


                return sslStream;
            };

        }


        public static Func<Stream, string, Task<Stream>> CreateDnsLocalStream()
        {
            return (stream, host) => Task.FromResult(stream);
        }


        public static Func<Task<Stream>> CreateDnsRemoteStream(string host, int port)
        {
            return async () =>
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                await socket.ConnectAsync(host, port).ConfigureAwait(false);

                return new NetworkStream(socket, true);
            };
        }
    }





    public static class ServerInfo
    {
        public static byte[] AdPageCer { get; set; }

        public static byte[] AdVideo { get; set; }

        public static byte[] MainPageCer { get; set; }

        public static byte[] IwaraCer { get; set; }

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





            const string PORNHUB_HOST = "www.livehub.com";

            const string IWARA_HOST = "iwara.tv";


            var pornhubListensEndPoint = ServerInfo.PornhubProxyEndPoint;
            var pacListensEndPoint = ServerInfo.PacServerEndPoint;
            var adErrorEndpoint = ServerInfo.ADErrorEndPoint;
            var iwaraLsitensPoint = ServerInfo.IwaraProxyEndPoint;


            var mainCert = new X509Certificate2(ServerInfo.MainPageCer);
            var adCert = new X509Certificate2(ServerInfo.AdPageCer);
            var iwaraCert = new X509Certificate2(ServerInfo.IwaraCer);
            var adVido = ServerInfo.AdVideo;


            PacServer pacServer = PacServer.Start(pacListensEndPoint,
                PacHelper.Create((host) => host == "www.pornhub.com", ProxyMode.CreateHTTP(adErrorEndpoint)),
                PacHelper.Create((host) => host == "hubt.pornhub.com", ProxyMode.CreateHTTP(adErrorEndpoint)),
                PacHelper.Create((host) => host == "ajax.googleapis.com", ProxyMode.CreateHTTP(adErrorEndpoint)),
                PacHelper.Create((host) => PacMethod.dnsDomainIs(host, "pornhub.com"), ProxyMode.CreateHTTP(pornhubListensEndPoint)),
                PacHelper.Create((host) => PacMethod.dnsDomainIs(host, "adtng.com"), ProxyMode.CreateHTTP(pornhubListensEndPoint)),
                PacHelper.Create((host) => PacMethod.dnsDomainIs(host, IWARA_HOST), ProxyMode.CreateHTTP(iwaraLsitensPoint)));


            
            PornhubProxyInfo info = new PornhubProxyInfo
            {
                MainPageStreamCreate = Connect.CreateLocalStream(new X509Certificate2(mainCert), SslProtocols.Tls12),

                ADPageStreamCreate = Connect.CreateLocalStream(new X509Certificate2(adCert), SslProtocols.Tls12),

                RemoteStreamCreate = Connect.CreateRemoteStream(PORNHUB_HOST, 443, PORNHUB_HOST, (a, b) => new MHttpStream(a, b), SslProtocols.Tls12),

                MaxContentSize = 1024 * 1024 * 5,

                ADVideoBytes = adVido,

                CheckingVideoHtml = Connect.CheckingVideoHtml,

                MaxRefreshRequestCount = 30,

                ReplaceResponseHtml = Connect.ReplaceResponseHtml,

            };

            PornhubProxyServer server = new PornhubProxyServer(info);


            Task t1 = server.Start(pornhubListensEndPoint);

            SniProxyInfo iwaraSniInfo = new SniProxyInfo(
                 iwaraLsitensPoint,
                 Connect.CreateDnsLocalStream(),
                 Connect.CreateDnsRemoteStream("104.20.27.25", 443));

            SniProxy iwaraSniProxy = new SniProxy(iwaraSniInfo);

            Task t2 = iwaraSniProxy.Start();

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

            ServerInfo.IwaraCer = MainActivity.ReadAllBytes(() => assets.Open("iwara.pfx"));

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