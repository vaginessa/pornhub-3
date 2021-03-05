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
    public sealed class HtmlPageData
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

    public sealed class Load
    {
        readonly Regex m_regex = new Regex(@"<a href=""([^""]+)""><img src=""([^""]+)"" width=""\d{3}"" height=""\d{3}"" alt=""([^""]+)""");

        const string HOST_URI = "https://ecchi.iwara.tv";

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
            string html = await load.GetStringAsync(func(), default).ConfigureAwait(false);


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


            Func<Uri> func = () => CreteNextUri(Interlocked.Increment(ref pages));

           


            var load = new Load();
            MHttpClient htmlLoad = new MHttpClient(new MHttpClientHandler
            {
                StreamCallback = MHttpClientHandler.CreateNewConnectAsync(
                     MHttpClientHandler.CreateCreateConnectAsyncFunc("konachan.com", 443),

                     MHttpClientHandler.CreateCreateAuthenticateAsyncFunc("iwara.tv", false)
                     )
            });



            foreach (var item in Enumerable.Range(0, taskCount))
            {
                Task.Run(() => load.LoadData(channel, htmlLoad, func));
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

        public IwaraPage(IwaraPageInfo info)
        {
            InitializeComponent();


            DeviceDisplay.KeepScreenOn = true;


            var coll = new ObservableCollection<DateBind>();

            m_collectionView.ItemsSource = coll;

            SetClo(2);

            m_load = Load.Create(6, 80, info.Pages);

            m_info = info;


            Task.Run(async () =>
            {
                var read = m_load.Reader;


                while (true)
                {
                    var item = await read.ReadAsync().ConfigureAwait(false);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        coll.Add(item);


                    });

                    await Task.Delay(new TimeSpan(0, 0, 1));
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

            return base.OnBackButtonPressed();
        }

        private void OnScrolled(object sender, ItemsViewScrolledEventArgs e)
        {

        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(m_collectionView.SelectedItem is DateBind date)
            {

                Xamarin.Essentials.Clipboard.SetTextAsync(date.Uri.AbsoluteUri);
            }

        }
    }
}