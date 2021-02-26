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

namespace pornhub.Droid
{


    public sealed class Source
    {
        public byte[] AdPageCer { get; set; }

        public byte[] AdVideo { get; set; }

        public byte[] MainPageCer { get; set; }
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


        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {


            return StartCommandResult.NotSticky;
        }


        


        void StartProxy()
        {
            var assets = this.Assets;

            Source source = new Source
            {
                AdVideo = MainActivity.ReadAllBytes(() => assets.Open("ad_video.mp4")),

                AdPageCer = MainActivity.ReadAllBytes(() => assets.Open("ad.com.pfx")),

                MainPageCer = MainActivity.ReadAllBytes(() => assets.Open("main.com.pfx")),

            };

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
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
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
                            .CreateNotificationChannel(new NotificationChannel(channelID, channelName, NotificationImportance.Default));
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
        public static byte[] ReadAllBytes(Func<Stream> func)
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

        public const string CHANNEL_ID = "fdfserte54354";

        public const string CHANNEL_NAME = "PROXY";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);
            


            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);


            ServerHelper.CreateNotificationChannel(this, CHANNEL_ID, CHANNEL_NAME);

            LoadApplication(new App(new EventClicked
            {
                CopyPacUriTo = () =>
                {

                    //Xamarin.Essentials.Clipboard.SetTextAsync(pacServer.ProxyUri.AbsoluteUri);
                },

                ExportCa = () =>
                {

                    Permissions.RequestAsync<Permissions.StorageWrite>()
                    .ContinueWith((task) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (task.Result == PermissionStatus.Granted)
                            {

                                File.WriteAllBytes(Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "pornhubCa.crt"), new X509Certificate2(ReadAllBytes(() => this.Assets.Open("myCA.pfx")), string.Empty, X509KeyStorageFlags.Exportable).Export(X509ContentType.Cert));
                            }

                        });
                    });
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

            }));    
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}