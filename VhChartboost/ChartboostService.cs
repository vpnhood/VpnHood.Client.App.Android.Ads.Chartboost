using Android.Content;
using VpnHood.Client.App.Abstractions;
using VpnHood.Client.Device;
using VpnHood.Client.Device.Droid;
using VpnHood.Common.Exceptions;
using VpnHood.Common.Utils;
using Com.Chartboost.Sdk;
using Com.Chartboost.Sdk.Ads;
using Com.Chartboost.Sdk.Callbacks;
using Com.Chartboost.Sdk.Events;
using Com.Chartboost.Sdk.Privacy.Model;

namespace VpnHood.Client.App.Droid.Ads.VhChartboost;

public class ChartboostService(string appId, string adSignature, string adLocation, Mediation? mediation = null) : IAppAdService
{
    private Interstitial _chartboostInterstitialAd;
    private MyInterstitialCallBack _myInterstitialCallBack;
    private static bool _isSdkStarted;
    public string NetworkName => "Chartboost";
    public AppAdType AdType => AppAdType.InterstitialAd;
    public DateTime? AdLoadedTime { get; private set; }

    public static ChartboostService Create(string appId, string adSignature, string adLocation, Mediation? mediation = null)
    {
        var ret = new ChartboostService(appId, adSignature, adLocation, mediation);
        return ret;
    }

    public bool IsCountrySupported(string countryCode)
    {
        return countryCode != "IR";
    }

    private async Task EnsureChartboostInitialized(Context context, CancellationToken cancellationToken)
    {
        if (_isSdkStarted)
            return;

        Chartboost.AddDataUseConsent(context, new COPPA(false));

        var onSdkStarted = new OnStarted();
        Chartboost.StartWithAppId(context, appId, adSignature, onSdkStarted);

        var cancellationTask = new TaskCompletionSource();
        cancellationToken.Register(cancellationTask.SetResult);
        await Task.WhenAny(onSdkStarted.Task, cancellationTask.Task).VhConfigureAwait();
        cancellationToken.ThrowIfCancellationRequested();

        await onSdkStarted.Task.VhConfigureAwait();
        _isSdkStarted = Chartboost.IsSdkStarted;
    }

    public async Task LoadAd(IUiContext uiContext, CancellationToken cancellationToken)
    {
        var appUiContext = (AndroidUiContext)uiContext;
        var activity = appUiContext.Activity;
        if (activity.IsDestroyed)
            throw new AdException("MainActivity has been destroyed before loading the ad.");

        // Initialize
        await EnsureChartboostInitialized(activity, cancellationToken);

        if (_chartboostInterstitialAd is { IsCached: true })
            return;

        // reset the last loaded ad
        AdLoadedTime = null;

        // Load a new Ad
        _myInterstitialCallBack = new MyInterstitialCallBack();
        _chartboostInterstitialAd = new Interstitial(adLocation, _myInterstitialCallBack, null);
        _chartboostInterstitialAd.Cache();

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

        if (AdLoadedTime == null)
            throw new AdException($"The {AdType} has not been loaded.");

        try
        {
            _chartboostInterstitialAd.Show();

            // wait for show or dismiss
            var cancellationTask = new TaskCompletionSource();
            cancellationToken.Register(cancellationTask.SetResult);
            await Task.WhenAny(_myInterstitialCallBack.ShownTask, cancellationTask.Task).VhConfigureAwait();
            cancellationToken.ThrowIfCancellationRequested();

            await _myInterstitialCallBack.ShownTask.VhConfigureAwait();
        }
        finally
        {
            _chartboostInterstitialAd.ClearCache();
            AdLoadedTime = null;
        }
    }

    private class OnStarted() : Java.Lang.Object, IStartCallback
    {
        private readonly TaskCompletionSource _initCompletionSource = new();
        public Task Task => _initCompletionSource.Task;
        public void OnStartCompleted(StartError? error)
        {
            if (error != null)
                _initCompletionSource.TrySetException(new LoadAdException(
                    $"Chartboost Ads initialization failed. Error: {error}, ErrorCode: {error.GetCode()}"));

            else
                _initCompletionSource.TrySetResult();
        }
    }

    private class MyInterstitialCallBack() : Java.Lang.Object, IInterstitialCallback
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
                    $"Unity Ads initialization failed. Error: {error}, ErrorCode: {error.GetCode()}"));
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