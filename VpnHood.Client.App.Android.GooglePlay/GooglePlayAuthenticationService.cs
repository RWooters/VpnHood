using System.Security.Authentication;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using VpnHood.Client.App.Abstractions;
using VpnHood.Client.App.Droid.Common;
using VpnHood.Client.Device.Droid.Utils;

namespace VpnHood.Client.App.Droid.GooglePlay;

public class GooglePlayAuthenticationService(string firebaseClientId) 
    : IAppAuthenticationExternalService
{
    private const int SignInIntentId = 20200;
    private TaskCompletionSource<GoogleSignInAccount>? _taskCompletionSource;
    private readonly GoogleSignInOptions _googleSignInOptions = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
        .RequestIdToken(firebaseClientId)
        .RequestEmail()
        .Build();

    public async Task<string> SilentSignIn(IAppUiContext uiContext)
    {
        var appUiContext = (AndroidAppUiContext)uiContext;

        using var googleSignInClient = GoogleSignIn.GetClient(appUiContext.Activity, _googleSignInOptions);
        var account = await googleSignInClient.SilentSignInAsync();
        return account?.IdToken ?? throw new AuthenticationException("Could not perform SilentSignIn by Google.");
    }

    public async Task<string> SignIn(IAppUiContext uiContext)
    {
        var appUiContext = (AndroidAppUiContext)uiContext;

        try
        {
            using var googleSignInClient = GoogleSignIn.GetClient(appUiContext.Activity, _googleSignInOptions);

            _taskCompletionSource = new TaskCompletionSource<GoogleSignInAccount>();
            appUiContext.ActivityEvent.ActivityResultEvent += Activity_OnActivityResult;
            appUiContext.ActivityEvent.Activity.StartActivityForResult(googleSignInClient.SignInIntent, SignInIntentId);
            var account = await _taskCompletionSource.Task;

            if (account.IdToken == null)
                throw new ArgumentNullException(account.IdToken);

            return account.IdToken;
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 12501)
                throw new OperationCanceledException();
            throw;
        }
        finally
        {
            appUiContext.ActivityEvent.ActivityResultEvent -= Activity_OnActivityResult;
        }
        
    }

    public async Task SignOut(IAppUiContext uiContext)
    {
        var appUiContext = (AndroidAppUiContext)uiContext;
        using var googleSignInClient = GoogleSignIn.GetClient(appUiContext.Activity, _googleSignInOptions);
        await googleSignInClient.SignOutAsync();
    }

    private void Activity_OnActivityResult(object? sender, ActivityResultEventArgs e)
    {
        // If the request code is not related to the Google sign-in method
        if (e.RequestCode == SignInIntentId)
            _ = ProcessSignedInAccountFromIntent(e.Data);
    }

    private async Task ProcessSignedInAccountFromIntent(Intent? intent)
    {
        try
        {
            var googleSignInAccount = await GoogleSignIn.GetSignedInAccountFromIntentAsync(intent);
            _taskCompletionSource?.SetResult(googleSignInAccount);
        }
        catch (Exception e)
        {
            _taskCompletionSource?.TrySetException(e);
        }
    }
    public void Dispose()
    {
    }
}