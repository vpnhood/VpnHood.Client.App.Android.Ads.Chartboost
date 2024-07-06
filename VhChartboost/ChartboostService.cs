using VpnHood.Client.App.Abstractions;
using VpnHood.Client.Device;
using VpnHood.Client.Device.Droid;
using VpnHood.Common.Exceptions;
using VpnHood.Common.Utils;
using Com.Chartboost.Sdk.Ads;
using Com.Chartboost.Sdk.Callbacks;
using Com.Chartboost.Sdk.Events;

namespace VpnHood.Client.App.Droid.Ads.VhChartboost;

public class ChartboostService(string appId, string adSignature, string adLocation) : IAppAdService
{
    private Interstitial? _chartboostInterstitialAd;
    private MyInterstitialCallBack? _myInterstitialCallBack;
    public string NetworkName => "Chartboost";
    public AppAdType AdType => AppAdType.InterstitialAd;
    public DateTime? AdLoadedTime { get; private set; }
    public TimeSpan AdLifeSpan { get; } = TimeSpan.FromMinutes(45);

    public static ChartboostService Create(string appId, string adSignature, string adLocation)
    {
        var ret = new ChartboostService(appId, adSignature, adLocation);
        return ret;
    }

    public bool IsCountrySupported(string countryCode)
    {
        // Make sure it is upper case
        countryCode = countryCode.Trim().ToUpper(); 

        // these countries are not supported at all
        if (countryCode == "CN")
            return false;

        // these countries video ad is not supported
        if (hasVideo)
            return countryCode != "IR";

		return true;
    }

    public async Task LoadAd(IUiContext uiContext, CancellationToken cancellationToken)
    {
        var appUiContext = (AndroidUiContext)uiContext;
        var activity = appUiContext.Activity;
        if (activity.IsDestroyed)
            throw new AdException("MainActivity has been destroyed before loading the ad.");

        // initialize
        await ChartboostUtil.Initialize(activity, appId, adSignature, cancellationToken);

        // reset the last loaded ad
        AdLoadedTime = null;

        // Load a new Ad
        _myInterstitialCallBack = new MyInterstitialCallBack();
        activity.RunOnUiThread(() =>
        {
            _chartboostInterstitialAd = new Interstitial(adLocation, _myInterstitialCallBack, null);
            _chartboostInterstitialAd.Cache();
        });

        var cancellationTask = new TaskCompletionSource();
        cancellationToken.Register(cancellationTask.SetResult);
        await Task.WhenAny(_myInterstitialCallBack.LoadTask, cancellationTask.Task).VhConfigureAwait();
        cancellationToken.ThrowIfCancellationRequested();

        await _myInterstitialCallBack.LoadTask.VhConfigureAwait();
        AdLoadedTime = DateTime.Now;
    }

    public async Task ShowAd(IUiContext uiContext, string? customData, CancellationToken cancellationToken)
    {
        var appUiContext = (AndroidUiContext)uiContext;
        var activity = appUiContext.Activity;
        if (activity.IsDestroyed)
            throw new AdException("MainActivity has been destroyed before showing the ad.");

        try
        {
            if (AdLoadedTime == null || _chartboostInterstitialAd == null || _myInterstitialCallBack == null)
                throw new AdException($"The {AdType} has not been loaded.");

            activity.RunOnUiThread(() =>
            {
                _chartboostInterstitialAd.Show(); 
            });

            // wait for show or dismiss
            var cancellationTask = new TaskCompletionSource();
            cancellationToken.Register(cancellationTask.SetResult);
            await Task.WhenAny(_myInterstitialCallBack.ShownTask, cancellationTask.Task).VhConfigureAwait();
            cancellationToken.ThrowIfCancellationRequested();

            await _myInterstitialCallBack.ShownTask.VhConfigureAwait();
        }
        finally
        {
            _chartboostInterstitialAd?.ClearCache();
            _chartboostInterstitialAd = null;
            AdLoadedTime = null;
        }
    }

    private class MyInterstitialCallBack : Java.Lang.Object, IInterstitialCallback
    {
        private readonly TaskCompletionSource _loadedCompletionSource = new();
        public Task LoadTask => _loadedCompletionSource.Task;

        private readonly TaskCompletionSource _shownCompletionSource = new();
        public Task ShownTask => _shownCompletionSource.Task;

        public void OnAdClicked(ClickEvent e, ClickError? error)
        {
        }

        public void OnAdLoaded(CacheEvent e, CacheError? error)
        {
            if (error != null)
                _loadedCompletionSource.TrySetException(new LoadAdException(
                    $"Chartboost Ads initialization failed. Error: {error}, ErrorCode: {error.GetCode()}"));
            else
                _loadedCompletionSource.TrySetResult();
        }

        public void OnAdRequestedToShow(ShowEvent e)
        {

        }

        public void OnAdShown(ShowEvent e, ShowError? error)
        {
            if (error != null)
                _shownCompletionSource.TrySetException(new LoadAdException(
                    $"Chartboost Ads show failed. Error: {error}, ErrorCode: {error.GetCode()}"));
        }

        public void OnImpressionRecorded(ImpressionEvent e)
        {

        }

        public void OnAdDismiss(DismissEvent e)
        {
            _shownCompletionSource.TrySetResult();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}