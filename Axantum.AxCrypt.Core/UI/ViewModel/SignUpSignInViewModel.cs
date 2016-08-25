﻿using Axantum.AxCrypt.Abstractions;
using Axantum.AxCrypt.Api.Model;
using Axantum.AxCrypt.Common;
using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.Extensions;
using Axantum.AxCrypt.Core.Runtime;
using Axantum.AxCrypt.Core.Service;
using AxCrypt.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static Axantum.AxCrypt.Abstractions.TypeResolve;

namespace Axantum.AxCrypt.Core.UI.ViewModel
{
    public sealed class SignUpSignInViewModel : ViewModelBase
    {
        public string UserEmail { get { return GetProperty<string>(nameof(UserEmail)); } set { SetProperty(nameof(UserEmail), value); } }

        public bool StopAndExit { get { return GetProperty<bool>(nameof(StopAndExit)); } set { SetProperty(nameof(StopAndExit), value); } }

        public bool TopControlsEnabled { get { return GetProperty<bool>(nameof(TopControlsEnabled)); } set { SetProperty(nameof(TopControlsEnabled), value); } }

        public ApiVersion Version { get { return GetProperty<ApiVersion>(nameof(Version)); } set { SetProperty(nameof(Version), value); } }

        public IAsyncAction DoDialogs { get { return new AsyncDelegateAction<object>((o) => DoDialogsActionAsync()); } }

        public IAsyncAction SignIn { get { return new AsyncDelegateAction<object>((o) => SignInActionAsync()); } }

        public event EventHandler<CancelEventArgs> CreateAccount;

        public event EventHandler<CancelEventArgs> VerifyAccount;

        public event EventHandler<CancelEventArgs> RequestEmail;

        public event EventHandler<CancelEventArgs> SigningIn;

        public event EventHandler RestoreWindow;

        private ISignInState _signInState;

        public SignUpSignInViewModel(ISignInState signInState)
        {
            _signInState = signInState;

            InitializePropertyValues();
            BindPropertyChangedEvents();
            SubscribeToModelEvents();
        }

        private void InitializePropertyValues()
        {
        }

        private void BindPropertyChangedEvents()
        {
        }

        private void SubscribeToModelEvents()
        {
        }

        private async Task SignInActionAsync()
        {
            CancelEventArgs e = new CancelEventArgs();
            OnRestoreWindow(e);
            if (_signInState.IsSigningIn)
            {
                return;
            }

            _signInState.IsSigningIn = true;
            try
            {
                do
                {
                    await WrapMessageDialogsAsync(async () =>
                    {
                        await DoDialogsActionAsync();
                        if (StopAndExit)
                        {
                            return;
                        }

                        OnSigningIn(e);
                        UserEmail = New<IUserSettings>().UserEmail;
                    });
                    if (StopAndExit)
                    {
                        return;
                    }
                } while (String.IsNullOrEmpty(UserEmail));
            }
            finally
            {
                _signInState.IsSigningIn = false;
            }
            if (Resolve.UserSettings.IsFirstSignIn)
            {
                New<IPopup>().Show(PopupButtons.Ok, Texts.InformationTitle, Texts.InternetNotRequiredInformation);
                Resolve.UserSettings.IsFirstSignIn = false;
            }
        }

        private async Task WrapMessageDialogsAsync(Func<Task> dialogFunctionAsync)
        {
            TopControlsEnabled = false;
            if (Version != ApiVersion.Zero && Version != new ApiVersion())
            {
                New<IPopup>().Show(PopupButtons.Ok, Texts.MessageServerUpdateTitle, Texts.MessageServerUpdateText);
            }
            while (true)
            {
                try
                {
                    await dialogFunctionAsync();
                    if (StopAndExit)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ApplicationExitException)
                    {
                        throw;
                    }
                    New<IReport>().Exception(ex);

                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }
                    New<IPopup>().Show(PopupButtons.Ok, Texts.MessageUnexpectedErrorTitle, Texts.MessageUnexpectedErrorText.InvariantFormat(ex.Message));
                    continue;
                }
                finally
                {
                    TopControlsEnabled = true;
                }
                break;
            }
            return;
        }

        public async Task DoDialogsActionAsync()
        {
            AccountStatus status;
            PopupButtons dialogResult;
            do
            {
                dialogResult = PopupButtons.Ok;
                status = await EnsureEmailAccountAsync();
                if (StopAndExit)
                {
                    return;
                }

                switch (status)
                {
                    case AccountStatus.NotFound:
                        await New<LogOnIdentity, IAccountService>(LogOnIdentity.Empty).SignupAsync(EmailAddress.Parse(UserEmail));
                        New<IPopup>().Show(PopupButtons.Ok, Texts.MessageSigningUpTitle, Texts.MessageSigningUpText.InvariantFormat(UserEmail));
                        status = VerifyAccountOnline();
                        break;

                    case AccountStatus.InvalidName:
                        dialogResult = New<IPopup>().Show(PopupButtons.OkCancelExit, Texts.MessageInvalidSignUpEmailTitle, Texts.MessageInvalidSignUpEmailText.InvariantFormat(UserEmail));
                        UserEmail = string.Empty;
                        break;

                    case AccountStatus.Unverified:
                        status = VerifyAccountOnline();
                        break;

                    case AccountStatus.Verified:
                        break;

                    case AccountStatus.Offline:
                        New<AxCryptOnlineState>().IsOnline = New<IInternetState>().Connected;
                        CancelEventArgs e = new CancelEventArgs();
                        OnCreateAccount(e);
                        if (!e.Cancel)
                        {
                            status = AccountStatus.Verified;
                        }
                        break;

                    case AccountStatus.Unknown:
                    case AccountStatus.Unauthenticated:
                    case AccountStatus.DefinedByServer:
                        dialogResult = New<IPopup>().Show(PopupButtons.OkCancelExit, Texts.MessageUnexpectedErrorTitle, Texts.MessageUnexpectedErrorText);
                        UserEmail = string.Empty;
                        break;

                    default:
                        break;
                }
                if (dialogResult == PopupButtons.Exit)
                {
                    StopAndExit = true;
                    return;
                }
                if (dialogResult == PopupButtons.Cancel)
                {
                    return;
                }
            } while (status != AccountStatus.Verified);
        }

        private async Task<AccountStatus> EnsureEmailAccountAsync()
        {
            AccountStatus status;
            if (!string.IsNullOrEmpty(UserEmail))
            {
                status = await GetCurrentStatusAsync();
                switch (status)
                {
                    case AccountStatus.Unknown:
                    case AccountStatus.InvalidName:
                    case AccountStatus.Unverified:
                    case AccountStatus.NotFound:
                    case AccountStatus.Offline:
                        break;

                    default:
                        return status;
                }
            }

            AskForEmailAddressToUse();
            if (StopAndExit)
            {
                return AccountStatus.Unknown;
            }

            status = await New<LogOnIdentity, IAccountService>(LogOnIdentity.Empty).StatusAsync(EmailAddress.Parse(UserEmail));
            return status;
        }

        private async Task<AccountStatus> GetCurrentStatusAsync()
        {
            EmailAddress email;
            if (!EmailAddress.TryParse(UserEmail, out email))
            {
                return AccountStatus.InvalidName;
            }
            AccountStatus status = await New<LogOnIdentity, IAccountService>(LogOnIdentity.Empty).StatusAsync(email);
            return status;
        }

        private void AskForEmailAddressToUse()
        {
            CancelEventArgs e = new CancelEventArgs();
            OnRequestEmail(e);
            if (e.Cancel)
            {
                StopAndExit = true;
            }
        }

        private AccountStatus VerifyAccountOnline()
        {
            CancelEventArgs e = new CancelEventArgs();
            OnVerifyAccount(e);
            if (e.Cancel)
            {
                return AccountStatus.Unverified;
            }

            PopupButtons result = New<IPopup>().Show(PopupButtons.OkCancel, Texts.WelcomeToAxCryptTitle, Texts.WelcomeToAxCrypt);
            if (result == PopupButtons.Ok)
            {
                using (ILauncher launcher = New<ILauncher>())
                {
                    launcher.Launch(Texts.LinkToGettingStarted);
                }
            }
            return AccountStatus.Verified;
        }

        private void OnCreateAccount(CancelEventArgs e)
        {
            CreateAccount?.Invoke(this, e);
        }

        private void OnVerifyAccount(CancelEventArgs e)
        {
            VerifyAccount?.Invoke(this, e);
        }

        private void OnRequestEmail(CancelEventArgs e)
        {
            RequestEmail?.Invoke(this, e);
        }

        private void OnSigningIn(CancelEventArgs e)
        {
            SigningIn?.Invoke(this, e);
        }

        private void OnRestoreWindow(EventArgs e)
        {
            RestoreWindow?.Invoke(this, e);
        }
    }
}