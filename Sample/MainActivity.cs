using VpnHood.Client.App.Droid.Ads.VhChartboost;
using VpnHood.Client.Device.Droid;
using VpnHood.Client.Device.Droid.ActivityEvents;

namespace Sample;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : ActivityEvent
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);
        _ = Foo();
    }

    private async Task Foo()
    {
        try
        {
            var adService = ChartboostService.Create("4f7b433509b6025804000002", "dd2d41b69ac01b80f443f5b6cf06096d457f82bd", "before_connect_interstitial");
            await adService.LoadAd(new AndroidUiContext(this), new CancellationToken());
            await adService.ShowAd(new AndroidUiContext(this), "", new CancellationToken());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}