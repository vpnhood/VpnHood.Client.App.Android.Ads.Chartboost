using Android.Content;
using Com.Chartboost.Sdk;
using Com.Chartboost.Sdk.Callbacks;
using Com.Chartboost.Sdk.Events;
using Com.Chartboost.Sdk.Privacy.Model;
using VpnHood.Common.Exceptions;
using VpnHood.Common.Utils;

namespace VpnHood.Client.App.Droid.Ads.VhChartboost;

public class ChartboostUtil
{
    private static readonly AsyncLock InitLock = new();
    public static bool IsInitialized { get; private set; }

    public static async Task Initialize(Context context, string appId, string adSignature,
        CancellationToken cancellationToken)
    {
        using var lockAsync = await InitLock.LockAsync(cancellationToken);
        if (IsInitialized)
            return;

        Chartboost.AddDataUseConsent(context, new COPPA(false));

        var sdkStartCallback = new StartCallback();
        Chartboost.StartWithAppId(context, appId, adSignature, sdkStartCallback);

        var cancellationTask = new TaskCompletionSource();
        cancellationToken.Register(cancellationTask.SetResult);
        await Task.WhenAny(sdkStartCallback.Task, cancellationTask.Task).VhConfigureAwait();
        cancellationToken.ThrowIfCancellationRequested();

        await sdkStartCallback.Task.VhConfigureAwait();
        IsInitialized = true;
    }

    private class StartCallback : Java.Lang.Object, IStartCallback
    {
        private readonly TaskCompletionSource _initCompletionSource = new();
        public Task Task => _initCompletionSource.Task;

        public void OnStartCompleted(StartError? error)
        {
            if (error != null)
                _initCompletionSource.TrySetException(
                    new LoadAdException(
                        $"Chartboost Ads initialization failed. Error: {error}, ErrorCode: {error.GetCode()}"));
            else
                _initCompletionSource.TrySetResult();
        }
    }
}
