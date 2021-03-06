using LeiKaiFeng.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.ObjectModel;


using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

namespace pornhub
{
    
    public sealed class Load
    {

        sealed class HtmlPageData
        {
            public HtmlPageData(Uri uri, Uri imgUri, string title)
            {
                Uri = uri;
                ImgUri = imgUri;
                Title = title;
            }

            public Uri Uri { get; }

            public Uri ImgUri { get; }

            public string Title { get; }

        }


        readonly Regex m_regex = new Regex(@"<a href=""([^""]+)""><img src=""([^""]+)"" width=""\d{3}"" height=""\d{3}"" alt=""([^""]+)""");

        const string HOST_URI = "https://ecchi.iwara.tv";

        const string SNI_HOST = "iwara.tv";

        const string DNS_HOST = "konachan.com";

        static Uri CreteNextUri(int n)
        {
            return new Uri("https://ecchi.iwara.tv/videos?language=en&sort=views&page=" + n);
        }


        List<HtmlPageData> CreateHtmlPageData(string html)
        {
            var list = new List<HtmlPageData>();

            var coll = m_regex.Matches(html);
            
            for (int i = 0; i < coll.Count; i++)
            {
                Match match = coll[i];

                Uri uri = new Uri(HOST_URI + match.Groups[1].Value);

                Uri imgUri = new Uri("https:" + match.Groups[2].Value);

                string title = match.Groups[3].Value;

              

                list.Add(new HtmlPageData(uri, imgUri, title));
            }

            return list;   
        }


        

        async Task LoadData(ChannelWriter<DateBind> writer, MHttpClient load, Func<Uri> func)
        {
            async Task AddDate(string html)
            {
                foreach (var item in CreateHtmlPageData(html))
                {

                    try
                    {

                        byte[] buffer = await load.GetByteArrayAsync(item.ImgUri, item.Uri, default).ConfigureAwait(false);

                        await writer.WriteAsync(new DateBind(
                            ImageSource.FromStream(() => new MemoryStream(buffer)),
                            item.Title,
                            item.Uri)).ConfigureAwait(false);



                    }
                    catch (MHttpClientException)
                    {

                    }
                }
            }


            while (true)
            {
                try
                {

                    string html = await load.GetStringAsync(func(), default).ConfigureAwait(false);

                    await AddDate(html).ConfigureAwait(false);
                }
                catch (MHttpClientException)
                {

                }
                catch (ChannelClosedException)
                {
                    return;
                }
                
            }

                      
        }

        public ChannelReader<DateBind> Reader { get; private set; }

        public Func<int> PageFunc { get; private set; }

        public Action Cannel { get; private set; }

        public static Load Create(int taskCount, int itemCount, int pages)
        {
            var channel = Channel.CreateBounded<DateBind>(itemCount);


            Func<Uri> getnexturi = () => CreteNextUri(Interlocked.Increment(ref pages));

           
            MHttpClient htmlLoad = new MHttpClient(new MHttpClientHandler
            {
                StreamCallback = MHttpClientHandler.CreateNewConnectAsync(
                     MHttpClientHandler.CreateCreateConnectAsyncFunc(DNS_HOST, 443),

                     MHttpClientHandler.CreateCreateAuthenticateAsyncFunc(SNI_HOST, false)
                     )
            });


            var load = new Load();

            foreach (var item in Enumerable.Range(0, taskCount))
            {
                Task.Run(() => load.LoadData(channel, htmlLoad, getnexturi));
            }

            return new Load()
            {
                Reader = channel,

                PageFunc = () => pages,

                Cannel = () => channel.Writer.TryComplete()
            };

        }
    }



    public sealed class DateBind
    {
        public DateBind(ImageSource imageSource, string title, Uri uri)
        {
            ImageSource = imageSource;
            Title = title;
            Uri = uri;
        }

        public ImageSource ImageSource { get; }

        public string Title { get; }

        public Uri Uri { get; }
    }


    public sealed class IwaraPageInfo
    {
        public IwaraPageInfo(int pages)
        {
            Source = new TaskCompletionSource<int>();
            Pages = pages;
       
        }

        TaskCompletionSource<int> Source { get; }

        public int Pages { get; }


        public Task<int> Task => Source.Task;


        public void Set(int n)
        {
            Source.TrySetResult(n);
        }
    }





    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class IwaraPage : ContentPage
    {
        Load m_load;

        IwaraPageInfo m_info;

        ManualResetEventSlim m_event = new ManualResetEventSlim(true);

        ObservableCollection<DateBind> m_coll = new ObservableCollection<DateBind>();

        public IwaraPage(IwaraPageInfo info)
        {
            InitializeComponent();

            SetClo(2);

            m_event.Set();

            DeviceDisplay.KeepScreenOn = true;

            m_collectionView.ItemsSource = m_coll;
            
            m_load = Load.Create(6, 120, info.Pages);

            m_info = info;

            Task.Run(async () =>
            {
                var read = m_load.Reader;


                while (true)
                {
                    try
                    {

                        m_event.Wait();

                        var item = await read.ReadAsync().ConfigureAwait(false);

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (m_coll.Count >= 400)
                            {
                                m_coll.Clear();
                            }

                            m_coll.Add(item);
                        });

                        await Task.Delay(new TimeSpan(0, 0, 1));
                    }
                    catch (ChannelClosedException)
                    {
                        return;
                    }

                }
            });
        }

        void SetClo(int viewColumn)
        {
            var v = new GridItemsLayout(viewColumn, ItemsLayoutOrientation.Vertical)
            {
                SnapPointsType = SnapPointsType.Mandatory,

                SnapPointsAlignment = SnapPointsAlignment.End
            };

            m_collectionView.ItemsLayout = v;
        }

        protected override bool OnBackButtonPressed()
        {
            m_load.Cannel();

            m_info.Set(m_load.PageFunc());

            m_event.Set();

            DeviceDisplay.KeepScreenOn = false;


            return base.OnBackButtonPressed();
        }

        private void OnScrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            long n = (long)e.VerticalDelta;

            if (n != 0)
            {
                if (n < 0)
                {
                    m_event.Reset();

                }
                else if (n > 0 && e.LastVisibleItemIndex + 1 == m_coll.Count)
                {
                    m_event.Set();

                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(m_collectionView.SelectedItem is DateBind date)
            {
                m_event.Reset();

                Xamarin.Essentials.Browser.OpenAsync(date.Uri.AbsoluteUri, BrowserLaunchMode.External);
            }

        }
    }
}