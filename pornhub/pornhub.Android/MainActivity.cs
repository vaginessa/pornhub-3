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

namespace pornhub.Droid
{
    
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


        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);
            


            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

            var assets = this.Assets;

            Source source = new Source
            {
                AdVideo = ReadAllBytes(() => assets.Open("ad_video.mp4")),

                AdPageCer = ReadAllBytes(() => assets.Open("ad.com.pfx")),

                MainPageCer = ReadAllBytes(() => assets.Open("main.com.pfx")),

                CaCer = ReadAllBytes(() => assets.Open("myCA.pfx")),

                RootPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath
            };


            LoadApplication(new App(source));     
        }



        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}