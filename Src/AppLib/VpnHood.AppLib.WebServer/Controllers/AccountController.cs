﻿using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using VpnHood.AppLib.Abstractions;
using VpnHood.AppLib.Services.Accounts;
using VpnHood.AppLib.WebServer.Api;
using VpnHood.Core.Client.Device.UiContexts;

namespace VpnHood.AppLib.WebServer.Controllers;

internal class AccountController : WebApiController, IAccountController
{
    private static AppAccountService AccountService =>
        VpnHoodApp.Instance.Services.AccountService ??
        throw new Exception("Account service is not available at this moment.");

    [Route(HttpVerbs.Get, "/")]
    public Task<AppAccount?> Get()
    {
        return VpnHoodApp.Instance.Services.AccountService != null
            ? VpnHoodApp.Instance.Services.AccountService.GetAccount()
            : Task.FromResult<AppAccount?>(null);
    }

    [Route(HttpVerbs.Post, "/refresh")]
    public Task Refresh()
    {
        return AccountService.Refresh();
    }

    [Route(HttpVerbs.Get, "/is-signin-with-google-supported")]
    public bool IsSigninWithGoogleSupported()
    {
        return VpnHoodApp.Instance.Services.AccountService?.AuthenticationService.IsSignInWithGoogleSupported ?? false;
    }

    [Route(HttpVerbs.Post, "/signin-with-google")]
    public Task SignInWithGoogle()
    {
        if (!AccountService.AuthenticationService.IsSignInWithGoogleSupported)
            throw new NotSupportedException("Sign in with Google is not supported.");

        return AccountService.AuthenticationService.SignInWithGoogle(AppUiContext.RequiredContext);
    }

    [Route(HttpVerbs.Post, "/sign-out")]
    public Task SignOut()
    {
        return AccountService.AuthenticationService.SignOut(AppUiContext.RequiredContext);
    }

    [Route(HttpVerbs.Get, "/subscriptions/{subscriptionId}/access-keys")]
    public Task<string[]> ListAccessKeys(string subscriptionId)
    {
        return AccountService.ListAccessKeys(subscriptionId);
    }
}