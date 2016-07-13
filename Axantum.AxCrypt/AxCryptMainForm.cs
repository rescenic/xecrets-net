﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2016, Svante Seleborg, All Rights Reserved
 *
 * This file is part of AxCrypt.
 *
 * AxCrypt is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * AxCrypt is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with AxCrypt.  If not, see <http://www.gnu.org/licenses/>.
 *
 * The source is maintained at http://bitbucket.org/axantum/axcrypt-net please visit for
 * updates, contributions and contact with the author. You may also visit
 * http://www.axcrypt.net for more information about the author.
*/

#endregion Coypright and License

using Axantum.AxCrypt.Abstractions;
using Axantum.AxCrypt.Api;
using Axantum.AxCrypt.Api.Model;
using Axantum.AxCrypt.Common;
using Axantum.AxCrypt.Core;
using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.Crypto.Asymmetric;
using Axantum.AxCrypt.Core.Extensions;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Ipc;
using Axantum.AxCrypt.Core.Runtime;
using Axantum.AxCrypt.Core.Service;
using Axantum.AxCrypt.Core.Session;
using Axantum.AxCrypt.Core.UI;
using Axantum.AxCrypt.Core.UI.ViewModel;
using Axantum.AxCrypt.Desktop;
using Axantum.AxCrypt.Forms.Implementation;
using Axantum.AxCrypt.Forms.Style;
using Axantum.AxCrypt.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Axantum.AxCrypt.Abstractions.TypeResolve;
using Texts = AxCrypt.Content.Texts;

namespace Axantum.AxCrypt
{
    /// <summary>
    /// All code here is expected to execute on the GUI thread. If code may be called on another thread, this call
    /// must be made through ThreadSafeUi() .
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Ax")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    public partial class AxCryptMainForm : Form, IStatusChecker
    {
        private NotifyIcon _notifyIcon = null;

        private MainViewModel _mainViewModel;

        private FileOperationViewModel _fileOperationViewModel;

        private KnownFoldersViewModel _knownFoldersViewModel;

        public static MessageBoxOptions MessageBoxOptions { get; private set; }

        private DebugLogOutputDialog _debugOutput;

        private TabPage _hiddenWatchedFoldersTabPage;

        private CommandLine _commandLine;

        private bool _startMinimized;

        public AxCryptMainForm()
        {
            InitializeComponent();
            new Styling(Resources.axcrypticon).Style(this, _recentFilesContextMenuStrip, _watchedFoldersContextMenuStrip);
        }

        public AxCryptMainForm(CommandLine commandLine)
            : this()
        {
            _commandLine = commandLine;
            _startMinimized = commandLine != null;
        }

        private void AxCryptMainForm_Load(object sender, EventArgs e)
        {
            if (DesignMode)
            {
                return;
            }

            try
            {
                InitializeProgram();
            }
            catch
            {
                ClearAllSettingsAndReinitialize();
                throw;
            }
        }

        private ApiVersion _apiVersion;

        private void InitializeProgram()
        {
            GetApiVersion();
            InitializeContentResources();
            RegisterTypeFactories();
            EnsureUiContextInitialized();

            if (!ValidateSettings())
            {
                return;
            }

            StartKeyPairService();
            SetupViewModels();
            AttachLogListener();
            ConfigureUiOptions();
            SetupPathFilters();
            IntializeControls();
            RestoreUserPreferences();
            BindToViewModels();
            BindToFileOperationViewModel();
            WireUpEvents();
            SetupCommandService();
            SendStartSessionNotification();
            StartupProcessMonitor();
            ExecuteCommandLine();
        }

        private void GetApiVersion()
        {
            _apiVersion = New<ICache>().GetItemAsync(CacheKey.RootKey.Subkey("WrapMessageDialogsAsync_ApiVersion"), () => New<GlobalApiClient>().ApiVersionAsync()).Result;
        }

        private static void EnsureUiContextInitialized()
        {
            New<IUIThread>().Yield();
        }

        private void InitializeContentResources()
        {
            SetCulture();

            _addSecureFolderToolStripMenuItem.Text = Texts.AddSecureFolderMenuItemText;
            _cleanDecryptedToolStripMenuItem.Text = Texts.CleanDecryptedToolStripMenuItemText;
            _closeAndRemoveOpenFilesToolStripButton.ToolTipText = Texts.CloseAndRemoveOpenFilesToolStripButtonToolTipText;
            _createAccountToolStripMenuItem.Text = Texts.CreateAccountToolStripMenuItemText;
            _createAccountToolStripMenuItem.ToolTipText = Texts.CreateAccountToolStripMenuItemToolTipText;
            _cryptoName.Text = Texts.CryptoNameText;
            _debugCheckVersionNowToolStripMenuItem.Text = Texts.DebugCheckVersionNowToolStripMenuItemText;
            _debugCryptoPolicyToolStripMenuItem.Text = Texts.DebugCryptoPolicyToolStripMenuItemText;
            _debugLoggingToolStripMenuItem.Text = Texts.DebugLoggingToolStripMenuItemText;
            _debugManageAccountToolStripMenuItem.Text = Texts.DebugManageAccountToolStripMenuItemText;
            _debugOpenReportToolStripMenuItem.Text = Texts.ReportSnapshotOpenMenuItem;
            _debugOptionsToolStripMenuItem.Text = Texts.DebugOptionsToolStripMenuItemText;
            _debugToolStripMenuItem.Text = Texts.DebugToolStripMenuItemText;
            _decryptAndRemoveFromListToolStripMenuItem.Text = Texts.DecryptAndRemoveFromListToolStripMenuItemText;
            _decryptedFileColumnHeader.Text = Texts.DecryptedFileColumnHeaderText;
            _decryptToolStripMenuItem.Text = Texts.DecryptToolStripMenuItemText;
            _encryptedFoldersToolStripMenuItem.Text = Texts.EncryptedFoldersToolStripMenuItemText;
            _encryptedPathColumnHeader.Text = Texts.EncryptedPathColumnHeaderText;
            _encryptToolStripButton.ToolTipText = Texts.EncryptToolStripButtonToolTipText;
            _encryptToolStripMenuItem.Text = Texts.EncryptToolStripMenuItemText;
            _englishLanguageToolStripMenuItem.Text = Texts.EnglishLanguageToolStripMenuItemText;
            _exitToolStripMenuItem.Text = Texts.ExitToolStripMenuItemText;
            _exportMyPrivateKeyToolStripMenuItem.Text = Texts.ExportMyPrivateKeyToolStripMenuItemText;
            _exportMyPrivateKeyToolStripMenuItem.ToolTipText = Texts.ExportMyPrivateKeyToolStripMenuItemToolTipText;
            _exportSharingKeyToolStripMenuItem.Text = Texts.ExportSharingKeyToolStripMenuItemText;
            _exportSharingKeyToolStripMenuItem.ToolTipText = Texts.ExportSharingKeyToolStripMenuItemToolTipText;
            _feedbackButton.Text = Texts.FeedbackButtonText;
            _fileToolStripMenuItem.Text = Texts.FileToolStripMenuItemText;
            _francaisLanguageToolStripMenuItem.Text = Texts.FrancaisLanguageToolStripMenuItemText;
            _germanLanguageToolStripMenuItem.Text = Texts.GermanLanguageSelectionText;
            _helpAboutToolStripMenuItem.Text = Texts.HelpAboutToolStripMenuItemText;
            _helpToolStripMenuItem.Text = Texts.HelpToolStripMenuItemText;
            _helpViewHelpMenuItem.Text = Texts.HelpViewHelpMenuItemText;
            _importMyPrivateKeyToolStripMenuItem.Text = Texts.ImportMyPrivateKeyToolStripMenuItemText;
            _importMyPrivateKeyToolStripMenuItem.ToolTipText = Texts.ImportMyPrivateKeyToolStripMenuItemToolTipText;
            _importOthersSharingKeyToolStripMenuItem.Text = Texts.ImportOthersSharingKeyToolStripMenuItemText;
            _importOthersSharingKeyToolStripMenuItem.ToolTipText = Texts.ImportOthersSharingKeyToolStripMenuItemToolTipText;
            _italianLanguageToolStripMenuItem.Text = Texts.ItalianLanguageSelection;
            _keyManagementToolStripMenuItem.Text = Texts.KeyManagementToolStripMenuItemText;
            _keyShareToolStripButton.ToolTipText = Texts.KeySharingToolTip;
            _koreanLanguageToolStripMenuItem.Text = Texts.KoreanLanguageSelection;
            _lastAccessTimeColumnHeader.Text = Texts.LastAccessTimeColumnHeaderText;
            _openEncryptedToolStripButton.ToolTipText = Texts.OpenToolStripButtonToolTipText;
            _openEncryptedToolStripMenuItem.Text = Texts.OpenEncryptedToolStripMenuItemText;
            _optionsAutoConvert1xFilesToolStripMenuItem.Text = Texts.OptionsConvertMenuItemText;
            _optionsAutoConvert1xFilesToolStripMenuItem.ToolTipText = Texts.OptionsConvertMenuToolTip;
            _optionsChangePassphraseToolStripMenuItem.Text = Texts.OptionsChangePassphraseToolStripMenuItemText;
            _optionsClearAllSettingsAndExitToolStripMenuItem.Text = Texts.OptionsClearAllSettingsAndExitToolStripMenuItemText;
            _optionsDebugToolStripMenuItem.Text = Texts.OptionsDebugToolStripMenuItemText;
            _optionsLanguageToolStripMenuItem.Text = Texts.OptionsLanguageToolStripMenuItemText;
            _optionsToolStripMenuItem.Text = Texts.OptionsToolStripMenuItemText;
            _progressContextCancelToolStripMenuItem.Text = Texts.ButtonCancelText;
            _recentFilesOpenToolStripMenuItem.Text = Texts.RecentFilesOpenToolStripMenuItemText;
            _recentFilesTabPage.Text = Texts.RecentFilesTabPageText;
            _removeRecentFileToolStripMenuItem.Text = Texts.RemoveRecentFileToolStripMenuItemText;
            _renameToolStripMenuItem.Text = Texts.AnonymousRenameMenuText;
            _renameToolStripMenuItem.ToolTipText = Texts.AnonymousRenameToolTip;
            _secretsToolStripButton.ToolTipText = Texts.SecretsButtonToolTipText;
            _secureDeleteToolStripMenuItem.Text = Texts.SecureDeleteToolStripMenuItemText;
            _shareKeysToolStripMenuItem.Text = Texts.ShareKeysToolStripMenuItemText;
            _signInToolStripMenuItem.Text = Texts.LogOnText;
            _signOutToolStripMenuItem.Text = Texts.LogOffText;
            _spanishLanguageToolStripMenuItem.Text = Texts.SpanishLanguageToolStripMenuItemText;
            _swedishLanguageToolStripMenuItem.Text = Texts.SwedishLanguageToolStripMenuItemText;
            _tryBrokenFileToolStripMenuItem.Text = Texts.TryBrokenFileToolStripMenuItemText;
            _upgradeLegacyMenuItem.Text = Texts.UpgradeLegacyFilesMenuItemText;
            _upgradeLegacyMenuItem.ToolTipText = Texts.UpgradeLegacyFilesMenuToolTip;
            _watchedFolderColumnHeader.Text = Texts.WatchedFolderColumnHeaderText;
            _watchedFoldersAddSecureFolderMenuItem.Text = Texts.AddSecureFolderMenuItemText;
            _watchedFoldersdecryptTemporarilyMenuItem.Text = Texts.MenuDecryptTemporarilyText;
            _watchedFoldersKeySharingMenuItem.Text = Texts.ShareKeysToolStripMenuItemText;
            _watchedFoldersOpenExplorerHereMenuItem.Text = Texts.WatchedFoldersOpenExplorerHereMenuItemText;
            _watchedFoldersRemoveMenuItem.Text = Texts.WatchedFoldersRemoveMenuItemText;
            _watchedFoldersTabPage.Text = Texts.WatchedFoldersTabPageText;
        }

        private static void SetCulture()
        {
            if (String.IsNullOrEmpty(Resolve.UserSettings.CultureName))
            {
                return;
            }
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(Resolve.UserSettings.CultureName);
        }

        private static void StartKeyPairService()
        {
            if (!String.IsNullOrEmpty(Resolve.UserSettings.UserEmail))
            {
                return;
            }
            New<KeyPairService>().Start();
        }

        private bool ValidateSettings()
        {
            if (Resolve.UserSettings.SettingsVersion >= Resolve.UserSettings.CurrentSettingsVersion)
            {
                return true;
            }

            Texts.UserSettingsFormatChangeNeedsReset.ShowWarning(this, Texts.WarningTitle);
            ClearAllSettingsAndReinitialize();
            StopAndExit();
            return false;
        }

        private async void AxCryptMainForm_ShownAsync(object sender, EventArgs e)
        {
            _pendingRequest = new CommandCompleteEventArgs(CommandVerb.Startup, new string[0]);
            if (_startMinimized)
            {
                ShowNotifyIcon();
                return;
            }
            await SignInAsync();
        }

        private bool _isSigningIn = false;

        private async Task SignInAsync()
        {
            RestoreWindowWithFocus();
            if (_isSigningIn)
            {
                return;
            }
            _isSigningIn = true;
            try
            {
                do
                {
                    await WrapMessageDialogsAsync(async () =>
                    {
                        SignUpSignIn signUpSignIn = new SignUpSignIn(Resolve.UserSettings.UserEmail);
                        await signUpSignIn.DialogsAsync(this);
                        Resolve.UserSettings.UserEmail = signUpSignIn.UserEmail;
                        if (signUpSignIn.StopAndExit)
                        {
                            StopAndExit();
                            return;
                        }

                        await _fileOperationViewModel.IdentityViewModel.LogOnAsync.ExecuteAsync(null);
                        await LogOnAndDoPendingRequestAsync();
                    });
                } while (String.IsNullOrEmpty(Resolve.UserSettings.UserEmail));
            }
            finally
            {
                _isSigningIn = false;
            }
            if (Resolve.UserSettings.IsFirstSignIn)
            {
                MessageDialog.ShowOk(this, Texts.InformationTitle, Texts.InternetNotRequiredInformation);
                Resolve.UserSettings.IsFirstSignIn = false;
            }
        }

        private async Task WrapMessageDialogsAsync(Func<Task> dialogFunctionAsync)
        {
            SetTopControlsEnabled(false);
            if (_apiVersion != ApiVersion.Zero && _apiVersion != new ApiVersion())
            {
                MessageDialog.ShowOk(this, Texts.MessageServerUpdateTitle, Texts.MessageServerUpdateText);
            }
            while (true)
            {
                try
                {
                    await dialogFunctionAsync();
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
                    MessageDialog.ShowOk(this, Texts.MessageUnexpectedErrorTitle, Texts.MessageUnexpectedErrorText.InvariantFormat(ex.Message));
                    continue;
                }
                SetTopControlsEnabled(true);
                break;
            }
            return;
        }

        private void SetTopControlsEnabled(bool enabled)
        {
            foreach (Control control in Controls)
            {
                control.Enabled = enabled;
            }
            Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;
        }

        private async Task LogOnAndDoPendingRequestAsync()
        {
            if (_mainViewModel.LoggedOn)
            {
                await DoRequestAsync(_pendingRequest);
            }
            _pendingRequest = new CommandCompleteEventArgs(CommandVerb.Unknown, new string[0]);
        }

        private static void SendStartSessionNotification()
        {
            Resolve.SessionNotify.Notify(new SessionNotification(SessionNotificationType.SessionStart));
        }

        private static void StartupProcessMonitor()
        {
            TypeMap.Register.Singleton(() => new ProcessMonitor());
            New<ProcessMonitor>();
        }

        private void ExecuteCommandLine()
        {
            if (_commandLine == null)
            {
                return;
            }

            Task.Run(() =>
            {
                _commandLine.Execute();
                _commandLine = null;
                ExplorerRefresh.Notify();
            });
        }

        private void SetupCommandService()
        {
            Resolve.CommandService.Received += New<CommandHandler>().RequestReceived;
            Resolve.CommandService.StartListening();
            New<CommandHandler>().CommandComplete += AxCryptMainForm_CommandComplete;
            New<IRuntimeEnvironment>().FirstInstanceIsReady();
        }

        private void ConfigureUiOptions()
        {
            MessageBoxOptions = RightToLeft == RightToLeft.Yes ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading : 0;
        }

        private void AttachLogListener()
        {
            Resolve.Log.Logged += (logger, loggingEventArgs) =>
            {
                Resolve.UIThread.PostTo(() =>
                {
                    if (_debugOutput == null || !_debugOutput.Visible)
                    {
                        return;
                    }
                    string formatted = "{0} {1}".InvariantFormat(New<INow>().Utc.ToString("o", CultureInfo.InvariantCulture), loggingEventArgs.Message.TrimLogMessage());
                    _debugOutput.AppendText(formatted);
                });
            };
        }

        private void SetupViewModels()
        {
            _mainViewModel = New<MainViewModel>();
            _fileOperationViewModel = New<FileOperationViewModel>();
            _knownFoldersViewModel = New<KnownFoldersViewModel>();
        }

        private void RegisterTypeFactories()
        {
            TypeMap.Register.Singleton<IUIThread>(() => new UIThread(this));
            TypeMap.Register.Singleton<IProgressBackground>(() => _progressBackgroundWorker);
            TypeMap.Register.Singleton<IStatusChecker>(() => this);
            TypeMap.Register.Singleton<IDataItemSelection>(() => new FileFolderSelection(this));

            TypeMap.Register.New<SessionNotificationHandler>(() => new SessionNotificationHandler(Resolve.FileSystemState, Resolve.KnownIdentities, New<ActiveFileAction>(), New<AxCryptFile>(), this));
            TypeMap.Register.New<IdentityViewModel>(() => new IdentityViewModel(Resolve.FileSystemState, Resolve.KnownIdentities, Resolve.UserSettings, Resolve.SessionNotify));
            TypeMap.Register.New<FileOperationViewModel>(() => new FileOperationViewModel(Resolve.FileSystemState, Resolve.SessionNotify, Resolve.KnownIdentities, Resolve.ParallelFileOperation, New<IStatusChecker>(), New<IdentityViewModel>()));
            TypeMap.Register.New<MainViewModel>(() => new MainViewModel(Resolve.FileSystemState, Resolve.UserSettings));
            TypeMap.Register.New<KnownFoldersViewModel>(() => new KnownFoldersViewModel(Resolve.FileSystemState, Resolve.SessionNotify, Resolve.KnownIdentities));
            TypeMap.Register.New<WatchedFoldersViewModel>(() => new WatchedFoldersViewModel(Resolve.FileSystemState));

            TypeMap.Register.Singleton<AboutBox>(() => new AboutBox());
        }

        private static void SetupPathFilters()
        {
            if (OS.Current.Platform != Platform.WindowsDesktop)
            {
                return;
            }

            OS.PathFilters.Add(new Regex(@"\\\.dropbox$"));
            OS.PathFilters.Add(new Regex(@"\\desktop\.ini$"));
            OS.PathFilters.Add(new Regex(@".*\.tmp$"));
            AddEnvironmentVariableBasedPathFilter(@"^{0}(?!Temp$)", "SystemRoot");
            AddEnvironmentVariableBasedPathFilter(@"^{0}(?!Temp$)", "windir");
            AddEnvironmentVariableBasedPathFilter(@"^{0}", "ProgramFiles");
            AddEnvironmentVariableBasedPathFilter(@"^{0}", "ProgramFiles(x86)");
            AddEnvironmentVariableBasedPathFilter(@"^{0}$", "SystemDrive");
        }

        private static void AddEnvironmentVariableBasedPathFilter(string formatRegularExpression, string name)
        {
            IDataContainer folder = name.FolderFromEnvironment();
            if (folder == null)
            {
                return;
            }
            string escapedPath = folder.FullName.Replace(@"\", @"\\");
            OS.PathFilters.Add(new Regex(formatRegularExpression.InvariantFormat(escapedPath)));
        }

        private void IntializeControls()
        {
            if (OS.Current.Platform == Platform.WindowsDesktop)
            {
                InitializeNotifyIcon();
            }

            ResizeEnd += (sender, e) =>
            {
                if (WindowState == FormWindowState.Normal)
                {
                    Preferences.MainWindowHeight = Height;
                    Preferences.MainWindowWidth = Width;
                }
            };
            Move += (sender, e) =>
            {
                if (WindowState == FormWindowState.Normal)
                {
                    Preferences.MainWindowLocation = Location;
                }
            };

            _encryptToolStripButton.Tag = FileInfoTypes.EncryptableFile;

            _hiddenWatchedFoldersTabPage = _statusTabControl.TabPages["_watchedFoldersTabPage"];

            _cleanDecryptedToolStripMenuItem.Click += CloseAndRemoveOpenFilesToolStripButton_Click;
            _closeAndRemoveOpenFilesToolStripButton.Click += CloseAndRemoveOpenFilesToolStripButton_Click;
            _feedbackButton.Click += (sender, e) => Process.Start(Texts.LinkToFeedbackWebPage);
            _optionsChangePassphraseToolStripMenuItem.Click += ChangePassphraseToolStripMenuItem_Click;
            _signInToolStripMenuItem.Click += async (sender, e) => await LogOnOrLogOffAndLogOnAgainAsync();
            _signOutToolStripMenuItem.Click += async (sender, e) => await LogOnOrLogOffAndLogOnAgainAsync();
            _softwareStatusButton.Click += _softwareStatusButton_Click;
#if DEBUG
            _debugCryptoPolicyToolStripMenuItem.Visible = true;
#endif
        }

        private void ConfigureMenusAccordingToPolicy(LicensePolicy license)
        {
            ConfigurePolicyMenu(license);
            ConfigureSecureWipe(license);
            ConfigureKeyShareMenus(license);
            ConfigureSecretsMenus(license);
            ConfigureAnonymousRename(license);
        }

        private void ConfigureKeyShareMenus(LicensePolicy license)
        {
            if (license.Has(LicenseCapability.KeySharing))
            {
                _keyShareToolStripButton.Image = Resources.share_border_80px;
                _keyShareToolStripButton.ToolTipText = Texts.KeySharingToolTip;
            }
            else
            {
                _keyShareToolStripButton.Image = Resources.share_border_grey_premium_80px;
                _keyShareToolStripButton.ToolTipText = Texts.PremiumNeededForKeyShare;
            }
        }

        private void ConfigureSecretsMenus(LicensePolicy license)
        {
            if (license.Has(LicenseCapability.PasswordManagement))
            {
                _secretsToolStripButton.Image = Resources.passwords_80px;
                _secretsToolStripButton.ToolTipText = Texts.SecretsButtonToolTipText;
            }
            else
            {
                _secretsToolStripButton.Image = Resources.passwords_grey_premium_80px;
                _secretsToolStripButton.ToolTipText = Texts.ToolTipPremiumNeededForSecrets;
            }
        }

        private void ConfigureSecureWipe(LicensePolicy license)
        {
            if (license.Has(LicenseCapability.SecureWipe))
            {
                _secureDeleteToolStripMenuItem.Image = Resources.delete;
                _secureDeleteToolStripMenuItem.ToolTipText = String.Empty;
            }
            else
            {
                _secureDeleteToolStripMenuItem.Image = Resources.premium_32px;
                _secureDeleteToolStripMenuItem.ToolTipText = Texts.PremiumFeatureToolTipText;
            }
        }

        private void ConfigureAnonymousRename(LicensePolicy license)
        {
            if (license.Has(LicenseCapability.RandomRename))
            {
                _renameToolStripMenuItem.Image = null;
                _renameToolStripMenuItem.ToolTipText = Texts.AnonymousRenameToolTip;
            }
            else
            {
                _renameToolStripMenuItem.Image = Resources.premium_32px;
                _renameToolStripMenuItem.ToolTipText = Texts.PremiumFeatureToolTipText;
            }
        }

        private void ConfigurePolicyMenu(LicensePolicy license)
        {
            ToolStripMenuItem item;
            _debugCryptoPolicyToolStripMenuItem.DropDownItems.Clear();

            item = new ToolStripMenuItem();
            item.Text = Texts.LicensePremiumNameText;
            item.Checked = license.Has(LicenseCapability.Premium);
            item.Click += PolicyMenuItem_Click;
            _debugCryptoPolicyToolStripMenuItem.DropDownItems.Add(item);

            item = new ToolStripMenuItem();
            item.Text = Texts.LicenseFreeNameText;
            item.Checked = !license.Has(LicenseCapability.Premium);
            item.Click += PolicyMenuItem_Click;
            _debugCryptoPolicyToolStripMenuItem.DropDownItems.Add(item);
        }

        private bool _balloonTipShown = false;

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon(components);
            _notifyIcon.Icon = Resources.axcrypticon;
            _notifyIcon.Text = Texts.AxCryptFileEncryption;
            _notifyIcon.BalloonTipTitle = Texts.AxCryptFileEncryption;
            _notifyIcon.BalloonTipText = Texts.TrayBalloonTooltip;
            _notifyIcon.Visible = false;

            _notifyIcon.MouseClick += (sender, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
            };

            Resize += (sender, e) =>
            {
                switch (WindowState)
                {
                    case FormWindowState.Minimized:
                        ShowNotifyIcon();
                        break;

                    case FormWindowState.Normal:
                        _notifyIcon.Visible = false;
                        break;
                }
            };
        }

        private void ShowNotifyIcon()
        {
            _notifyIcon.Visible = true;
            if (!_balloonTipShown)
            {
                _notifyIcon.ShowBalloonTip(500);
                _balloonTipShown = true;
            }
            Hide();
        }

        private void RestoreUserPreferences()
        {
            if (WindowState == FormWindowState.Normal)
            {
                Height = Preferences.MainWindowHeight.Fallback(Height);
                Width = Preferences.MainWindowWidth.Fallback(Width);
                Location = Preferences.MainWindowLocation.Fallback(Location).Safe();
            }

            _mainViewModel.RecentFilesComparer = GetComparer(Preferences.RecentFilesSortColumn, !Preferences.RecentFilesAscending);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void BindToViewModels()
        {
            _mainViewModel.Title = Texts.TitleMainWindow.InvariantFormat(Application.ProductName, Application.ProductVersion, String.IsNullOrEmpty(AboutBox.AssemblyDescription) ? String.Empty : " " + AboutBox.AssemblyDescription);

            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.DebugMode), (bool enabled) => { UpdateDebugMode(enabled); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.DecryptFileEnabled), (bool enabled) => { _decryptToolStripMenuItem.Enabled = enabled; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.DownloadVersion), (DownloadVersion dv) => { SetSoftwareStatus(); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.EncryptFileEnabled), (bool enabled) => { _encryptToolStripButton.Enabled = enabled; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.EncryptFileEnabled), (bool enabled) => { _encryptToolStripMenuItem.Enabled = enabled; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.FilesArePending), (bool filesArePending) => { _cleanDecryptedToolStripMenuItem.Enabled = filesArePending; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.FilesArePending), (bool filesArePending) => { _closeAndRemoveOpenFilesToolStripButton.Enabled = filesArePending; _closeAndRemoveOpenFilesToolStripButton.ToolTipText = filesArePending ? Texts.CloseAndRemoveOpenFilesToolStripButtonToolTipText : string.Empty; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.LegacyConversionMode), (LegacyConversionMode mode) => _optionsAutoConvert1xFilesToolStripMenuItem.Checked = mode == LegacyConversionMode.AutoConvertLegacyFiles);
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.License), (LicensePolicy license) => _knownFoldersViewModel.UpdateState.Execute(null));
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.License), (LicensePolicy license) => { ConfigureMenusAccordingToPolicy(license); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.License), (LicensePolicy license) => { SetDaysLeftWarning(); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.License), async (LicensePolicy license) => { await _recentFilesListView.UpdateRecentFilesAsync(_mainViewModel.RecentFiles); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.LoggedOn), (bool loggedOn) => { SetSignInSignOutStatus(loggedOn); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.OpenEncryptedEnabled), (bool enabled) => { _openEncryptedToolStripMenuItem.Enabled = enabled; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.RandomRenameEnabled), (bool enabled) => { _renameToolStripMenuItem.Enabled = enabled; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.RecentFiles), async (IEnumerable<ActiveFile> files) => { await _recentFilesListView.UpdateRecentFilesAsync(files); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.SelectedRecentFiles), (IEnumerable<string> files) => { _keyShareToolStripButton.Enabled = (files.Count() == 1 && _mainViewModel.LoggedOn) || !_mainViewModel.License.Has(LicenseCapability.KeySharing); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.TryBrokenFile), (bool enabled) => { _tryBrokenFileToolStripMenuItem.Checked = enabled; });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.WatchedFolders), (IEnumerable<string> folders) => { UpdateWatchedFolders(folders); });
            _mainViewModel.BindPropertyChanged(nameof(_mainViewModel.WatchedFoldersEnabled), (bool enabled) => { ConfigureWatchedFoldersMenus(enabled); });

            _daysLeftPremiumLabel.Click += (sender, e) => { Process.Start(Texts.LinkToAxCryptPremiumPurchasePage.QueryFormat(Resolve.UserSettings.AccountWebUrl, Resolve.KnownIdentities.DefaultEncryptionIdentity.UserEmail)); };
            _debugCheckVersionNowToolStripMenuItem.Click += (sender, e) => { _mainViewModel.AxCryptUpdateCheck.Execute(DateTime.MinValue); };
            _debugOpenReportToolStripMenuItem.Click += (sender, e) => { New<IReport>().Save(); New<IReport>().Open(); };
            _knownFoldersViewModel.BindPropertyChanged(nameof(_knownFoldersViewModel.KnownFolders), (IEnumerable<KnownFolder> folders) => UpdateKnownFolders(folders));
            _knownFoldersViewModel.KnownFolders = KnownFoldersDiscovery.Discover();
            _mainToolStrip.DragOver += (sender, e) => { _mainViewModel.DragAndDropFiles = e.GetDragged(); e.Effect = GetEffectsForMainToolStrip(e); };
            _optionsAutoConvert1xFilesToolStripMenuItem.Click += (sender, e) => ToggleLegacyConversion();
            _optionsClearAllSettingsAndExitToolStripMenuItem.Click += (sender, e) => { _mainViewModel.ClearPassphraseMemory.Execute(null); };
            _optionsDebugToolStripMenuItem.Click += (sender, e) => { _mainViewModel.DebugMode = !_mainViewModel.DebugMode; };
            _recentFilesListView.ColumnClick += (sender, e) => { SetSortOrder(e.Column); };
            _recentFilesListView.DragOver += (sender, e) => { _mainViewModel.DragAndDropFiles = e.GetDragged(); e.Effect = GetEffectsForRecentFiles(e); };
            _recentFilesListView.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Right) _recentFilesContextMenuStrip.Show((Control)sender, e.Location); };
            _recentFilesListView.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Right) _shareKeysToolStripMenuItem.Enabled = _mainViewModel.CanShare(_mainViewModel.SelectedRecentFiles.Select(srf => New<IDataStore>(srf))); };
            _recentFilesListView.SelectedIndexChanged += (sender, e) => { _mainViewModel.SelectedRecentFiles = _recentFilesListView.SelectedItems.Cast<ListViewItem>().Select(lvi => RecentFilesListView.EncryptedPath(lvi)); };
            _removeRecentFileToolStripMenuItem.Click += (sender, e) => { _mainViewModel.RemoveRecentFiles.Execute(_mainViewModel.SelectedRecentFiles); };
            _shareKeysToolStripMenuItem.Click += async (sender, e) => { await ShareKeysAsync(_mainViewModel.SelectedRecentFiles); };
            _watchedFoldersAddSecureFolderMenuItem.Click += async (sender, e) => { await PremiumFeature_ClickAsync(LicenseCapability.SecureFolders, (ss, ee) => { WatchedFoldersAddSecureFolderMenuItem_Click(ss, ee); return Task.FromResult<object>(null); }, sender, e); };
            _watchedFoldersKeySharingMenuItem.Click += async (sender, e) => { await PremiumFeature_ClickAsync(LicenseCapability.KeySharing, async (ss, ee) => { await WatchedFoldersKeySharingAsync(_mainViewModel.SelectedWatchedFolders); }, sender, e); };
            _watchedFoldersListView.DragDrop += async (sender, e) => { await PremiumFeature_ClickAsync(LicenseCapability.SecureFolders, (ss, ee) => { _mainViewModel.AddWatchedFolders.Execute(_mainViewModel.DragAndDropFiles); return Task.FromResult<object>(null); }, sender, e); };
            _watchedFoldersListView.DragOver += (sender, e) => { _mainViewModel.DragAndDropFiles = e.GetDragged(); e.Effect = GetEffectsForWatchedFolders(e); };
            _watchedFoldersListView.MouseDown += (sender, e) => { if (e.Button == MouseButtons.Right) { ShowHideWatchedFoldersContextMenuItems(e.Location); _watchedFoldersContextMenuStrip.Show((Control)sender, e.Location); } };
            _watchedFoldersListView.SelectedIndexChanged += (sender, e) => { _mainViewModel.SelectedWatchedFolders = _watchedFoldersListView.SelectedItems.Cast<ListViewItem>().Select(lvi => lvi.Text); };
            _watchedFoldersOpenExplorerHereMenuItem.Click += (sender, e) => { _mainViewModel.OpenSelectedFolder.Execute(_mainViewModel.SelectedWatchedFolders.First()); };
            _watchedFoldersRemoveMenuItem.Click += (sender, e) => { _mainViewModel.RemoveWatchedFolders.Execute(_mainViewModel.SelectedWatchedFolders); };
        }

        private void ConfigureWatchedFoldersMenus(bool enabled)
        {
            foreach (Control control in _hiddenWatchedFoldersTabPage.Controls)
            {
                control.Enabled = enabled;
            }
            _hiddenWatchedFoldersTabPage.ToolTipText = enabled ? string.Empty : Texts.PremiumFeatureToolTipText;

            if (enabled)
            {
                _encryptedFoldersToolStripMenuItem.ToolTipText = string.Empty;
                _encryptedFoldersToolStripMenuItem.Image = Resources.folder;
            }
            else
            {
                _encryptedFoldersToolStripMenuItem.ToolTipText = Texts.PremiumFeatureToolTipText;
                _encryptedFoldersToolStripMenuItem.Image = Resources.premium_32px;
            }

            if (enabled)
            {
                _addSecureFolderToolStripMenuItem.ToolTipText = string.Empty;
                _addSecureFolderToolStripMenuItem.Image = null;
            }
            else
            {
                _addSecureFolderToolStripMenuItem.ToolTipText = Texts.PremiumFeatureToolTipText;
                _addSecureFolderToolStripMenuItem.Image = Resources.premium_32px;
            }
        }

        private void ShowHideWatchedFoldersContextMenuItems(Point location)
        {
            bool itemSelected = _watchedFoldersListView.HitTest(location).Location == ListViewHitTestLocations.Label;
            _watchedFoldersdecryptTemporarilyMenuItem.Visible = itemSelected;
            _watchedFoldersOpenExplorerHereMenuItem.Visible = itemSelected;
            _watchedFoldersRemoveMenuItem.Visible = itemSelected;
            _watchedFoldersKeySharingMenuItem.Visible = itemSelected;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        private void BindToFileOperationViewModel()
        {
            _addSecureFolderToolStripMenuItem.Click += async (sender, e) => await PremiumFeature_ClickAsync(LicenseCapability.SecureFolders, (ss, ee) => { WatchedFoldersAddSecureFolderMenuItem_Click(ss, ee); return Task.FromResult<object>(null); }, sender, e);
            _decryptAndRemoveFromListToolStripMenuItem.Click += async (sender, e) => { await _fileOperationViewModel.DecryptFiles.ExecuteAsync(_mainViewModel.SelectedRecentFiles); };
            _decryptToolStripMenuItem.Click += async (sender, e) => { await _fileOperationViewModel.DecryptFiles.ExecuteAsync(null); };
            _encryptedFoldersToolStripMenuItem.Click += async (sender, e) => await PremiumFeature_ClickAsync(LicenseCapability.SecureFolders, (ss, ee) => { encryptedFoldersToolStripMenuItem_Click(ss, ee); return Task.FromResult<object>(null); }, sender, e);
            _encryptToolStripButton.Click += async (sender, e) => { await _fileOperationViewModel.EncryptFiles.ExecuteAsync(null); };
            _encryptToolStripButton.Tag = _fileOperationViewModel.EncryptFiles;
            _encryptToolStripMenuItem.Click += (sender, e) => _fileOperationViewModel.EncryptFiles.Execute(null);
            _fileOperationViewModel.FirstLegacyOpen += (sender, e) => New<IUIThread>().SendTo(() => SetLegacyOpenMode(e));
            _fileOperationViewModel.IdentityViewModel.LoggingOnAsync = async (e) => await New<IUIThread>().SendToAsync(async () => await HandleLogOn(e));
            _fileOperationViewModel.SelectingFiles += (sender, e) => New<IUIThread>().SendTo(() => New<IDataItemSelection>().HandleSelection(e));
            _fileOperationViewModel.ToggleLegacyConversion += (sender, e) => New<IUIThread>().SendTo(() => ToggleLegacyConversion());
            _keyShareToolStripButton.Click += async (sender, e) => { await PremiumFeature_ClickAsync(LicenseCapability.KeySharing, async (ss, ee) => { await ShareKeysAsync(_mainViewModel.SelectedRecentFiles); }, sender, e); };
            _openEncryptedToolStripButton.Click += async (sender, e) => { await _fileOperationViewModel.OpenFilesFromFolder.ExecuteAsync(string.Empty); };
            _openEncryptedToolStripMenuItem.Click += async (sender, e) => { await _fileOperationViewModel.OpenFilesFromFolder.ExecuteAsync(string.Empty); };
            _recentFilesListView.DragDrop += async (sender, e) => { await DropFilesOrFoldersInRecentFilesListViewAsync(); };
            _recentFilesListView.MouseDoubleClick += async (sender, e) => { await _fileOperationViewModel.OpenFiles.ExecuteAsync(_mainViewModel.SelectedRecentFiles); };
            _recentFilesOpenToolStripMenuItem.Click += async (sender, e) => { await _fileOperationViewModel.OpenFiles.ExecuteAsync(_mainViewModel.SelectedRecentFiles); };
            _renameToolStripMenuItem.Click += async (sender, e) => await PremiumFeature_ClickAsync(LicenseCapability.RandomRename, async (ss, ee) => { await _fileOperationViewModel.RandomRenameFiles.ExecuteAsync(null); }, sender, e);
            _secretsToolStripButton.Click += async (sender, e) => { await PremiumFeature_ClickAsync(LicenseCapability.PasswordManagement, (ss, ee) => { Process.Start(Texts.LinkToSecretsPageWithUserNameFormat.QueryFormat(Resolve.UserSettings.AccountWebUrl, Resolve.KnownIdentities.DefaultEncryptionIdentity.UserEmail)); return Task.FromResult<object>(null); }, sender, e); };
            _secureDeleteToolStripMenuItem.Click += async (sender, e) => await PremiumFeature_ClickAsync(LicenseCapability.SecureWipe, async (ss, ee) => { await _fileOperationViewModel.WipeFiles.ExecuteAsync(null); }, sender, e);
            _upgradeLegacyMenuItem.Click += async (sender, e) => await _fileOperationViewModel.AsyncUpgradeFiles.ExecuteAsync(null);
            _watchedFoldersdecryptTemporarilyMenuItem.Click += async (sender, e) => { await _fileOperationViewModel.DecryptFolders.ExecuteAsync(_mainViewModel.SelectedWatchedFolders); };
            _watchedFoldersListView.MouseDoubleClick += async (sender, e) => { await _fileOperationViewModel.OpenFilesFromFolder.ExecuteAsync(_mainViewModel.SelectedWatchedFolders.FirstOrDefault()); };
        }

        private void WireUpEvents()
        {
            Resolve.SessionNotify.Notification += async (sender, e) => await New<SessionNotificationHandler>().HandleNotificationAsync(e.Notification);

            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            New<AxCryptOnlineState>().OnlineStateChanged += (sender, e) =>
            {
                New<IUIThread>().PostTo(() =>
                {
                    SetWindowTextWithLogonStatus(_mainViewModel.LoggedOn);
                });
            };
        }

        private readonly object _autoCleanUpLock = new object();

        private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    await Task.Run(() =>
                    {
                        lock (_autoCleanUpLock)
                        {
                            EncryptPendingFiles();
                            WaitForBackgroundToComplete();
                        }
                    });
                    await LogOffAndLogOnAgainAsync();
                    break;

                default:
                    break;
            }
        }

        private async void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLogoff:
                    ShutDownAndExit();
                    break;

                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                case SessionSwitchReason.SessionLock:
                    await Task.Run(() =>
                    {
                        lock (_autoCleanUpLock)
                        {
                            EncryptPendingFiles();
                            WaitForBackgroundToComplete();
                        }
                    });
                    await LogOffAndLogOnAgainAsync();
                    break;

                default:
                    break;
            }
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionEndReasons.Logoff:
                case SessionEndReasons.SystemShutdown:
                    ShutDownAndExit();
                    break;

                default:
                    break;
            }
        }

        private void WireDownEvents()
        {
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        }

        private void SetSignInSignOutStatus(bool isSignedIn)
        {
            SetWindowTextWithLogonStatus(isSignedIn);

            bool isSignedInWithAxCryptId = isSignedIn && Resolve.KnownIdentities.DefaultEncryptionIdentity.UserKeys != null;

            _createAccountToolStripMenuItem.Enabled = !isSignedIn;
            _debugManageAccountToolStripMenuItem.Enabled = isSignedInWithAxCryptId;
            _exportMyPrivateKeyToolStripMenuItem.Enabled = isSignedInWithAxCryptId;
            _exportSharingKeyToolStripMenuItem.Enabled = isSignedInWithAxCryptId;
            _importMyPrivateKeyToolStripMenuItem.Enabled = !isSignedIn;
            _importOthersSharingKeyToolStripMenuItem.Enabled = isSignedInWithAxCryptId;
            _optionsAutoConvert1xFilesToolStripMenuItem.Enabled = isSignedInWithAxCryptId;
            _optionsChangePassphraseToolStripMenuItem.Enabled = isSignedInWithAxCryptId && New<AxCryptOnlineState>().IsOnline;
            _signInToolStripMenuItem.Visible = !isSignedIn;
            _signOutToolStripMenuItem.Visible = isSignedIn;
            _upgradeLegacyMenuItem.Enabled = isSignedInWithAxCryptId;
        }

        private void SetDaysLeftWarning()
        {
            LicensePolicy license = _mainViewModel.License;
            if (license.SubscriptionWarningTime == TimeSpan.MaxValue)
            {
                _daysLeftPremiumLabel.Visible = false;
                return;
            }

            if (license.SubscriptionWarningTime == TimeSpan.Zero)
            {
                _daysLeftPremiumLabel.Text = Texts.UpgradePromptText;
                _daysLeftPremiumLabel.LinkColor = Styling.WarningColor;
                _daysLeftToolTip.SetToolTip(_daysLeftPremiumLabel, Texts.NoPremiumWarning);
                _daysLeftPremiumLabel.Visible = true;
                return;
            }

            int days = license.SubscriptionWarningTime.Days;
            _daysLeftPremiumLabel.Text = (days > 1 ? Texts.DaysLeftPluralWarningPattern : Texts.DaysLeftSingularWarningPattern).InvariantFormat(days);
            _daysLeftPremiumLabel.LinkColor = Styling.WarningColor;
            _daysLeftToolTip.SetToolTip(_daysLeftPremiumLabel, Texts.DaysLeftWarningToolTip);
            _daysLeftPremiumLabel.Visible = true;
        }

        private void SetLegacyOpenMode(FileOperationEventArgs e)
        {
            if (!Resolve.KnownIdentities.IsLoggedOn)
            {
                return;
            }

            DialogResult dr = MessageDialog.ShowOkCancel(this, Texts.WarningTitle, Texts.LegacyOpenMessage);
            if (dr == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
        }

        private void ToggleLegacyConversion()
        {
            if (!Resolve.KnownIdentities.IsLoggedOn)
            {
                return;
            }

            if (_mainViewModel.LegacyConversionMode == LegacyConversionMode.AutoConvertLegacyFiles)
            {
                _mainViewModel.LegacyConversionMode = LegacyConversionMode.RetainLegacyFiles;
                return;
            }

            VerifySignInPasswordViewModel viewModel = new VerifySignInPasswordViewModel(Resolve.KnownIdentities.DefaultEncryptionIdentity);
            using (VerifySignInPasswordDialog dialog = new VerifySignInPasswordDialog(this, viewModel))
            {
                DialogResult dr = dialog.ShowDialog(this);
                if (dr != DialogResult.OK)
                {
                    _mainViewModel.LegacyConversionMode = LegacyConversionMode.NotDecided;
                    return;
                }
            }

            _mainViewModel.LegacyConversionMode = LegacyConversionMode.AutoConvertLegacyFiles;
        }

        private async Task LogOffAndLogOnAgainAsync()
        {
            if (!Resolve.KnownIdentities.IsLoggedOn)
            {
                return;
            }

            await LogOnOrLogOffAndLogOnAgainAsync();
        }

        private async Task LogOnOrLogOffAndLogOnAgainAsync()
        {
            bool wasLoggedOn = Resolve.KnownIdentities.IsLoggedOn;
            if (wasLoggedOn)
            {
                _fileOperationViewModel.IdentityViewModel.LogOnLogOff.Execute(null);
            }
            else
            {
                await SignInAsync();
            }
            bool didLogOff = wasLoggedOn && !Resolve.KnownIdentities.IsLoggedOn;
            if (didLogOff)
            {
                await SignInAsync();
            }
        }

        private async Task DropFilesOrFoldersInRecentFilesListViewAsync()
        {
            if (_mainViewModel.DroppableAsRecent)
            {
                await _fileOperationViewModel.AddRecentFiles.ExecuteAsync(_mainViewModel.DragAndDropFiles);
            }
            if (_mainViewModel.DroppableAsWatchedFolder)
            {
                ShowWatchedFolders(_mainViewModel.DragAndDropFiles);
            }
        }

        private Task HandleLogOn(LogOnEventArgs e)
        {
            if (e.IsAskingForPreviouslyUnknownPassphrase)
            {
                HandleCreateNewLogOn(e);
            }
            else
            {
                HandleExistingLogOn(e);
            }
            return Task.FromResult(default(object));
        }

        private void HandleCreateNewLogOn(LogOnEventArgs e)
        {
            RestoreWindowWithFocus();
            if (!String.IsNullOrEmpty(e.EncryptedFileFullName))
            {
                HandleCreateNewLogOnForEncryptedFile(e);
            }
            else
            {
                HandleCreateNewAccount(e);
            }
        }

        private void HandleCreateNewLogOnForEncryptedFile(LogOnEventArgs e)
        {
            using (NewPassphraseDialog passphraseDialog = new NewPassphraseDialog(this, Texts.NewPassphraseDialogTitle, e.Passphrase.Text, e.EncryptedFileFullName))
            {
                passphraseDialog.ShowPassphraseCheckBox.Checked = e.DisplayPassphrase;
                DialogResult dialogResult = passphraseDialog.ShowDialog(this);
                if (dialogResult != DialogResult.OK || passphraseDialog.PassphraseTextBox.Text.Length == 0)
                {
                    e.Cancel = true;
                    return;
                }
                e.DisplayPassphrase = passphraseDialog.ShowPassphraseCheckBox.Checked;
                e.Passphrase = new Passphrase(passphraseDialog.PassphraseTextBox.Text);
                e.Name = String.Empty;
            }
            return;
        }

        private void HandleCreateNewAccount(LogOnEventArgs e)
        {
            using (CreateNewAccountDialog dialog = new CreateNewAccountDialog(this, e.Passphrase.Text, EmailAddress.Empty))
            {
                DialogResult dialogResult = dialog.ShowDialog(this);
                if (dialogResult != DialogResult.OK)
                {
                    e.Cancel = true;
                    return;
                }
                e.DisplayPassphrase = dialog.ShowPassphraseCheckBox.Checked;
                e.Passphrase = new Passphrase(dialog.PassphraseTextBox.Text);
                e.UserEmail = dialog.EmailTextBox.Text;
            }
        }

        private void HandleExistingLogOn(LogOnEventArgs e)
        {
            RestoreWindowWithFocus();
            if (!String.IsNullOrEmpty(e.EncryptedFileFullName) && (String.IsNullOrEmpty(Resolve.UserSettings.UserEmail) || Resolve.KnownIdentities.IsLoggedOn))
            {
                HandleExistingLogOnForEncryptedFile(e);
            }
            else
            {
                HandleExistingAccountLogOn(e);
            }
        }

        private void HandleExistingLogOnForEncryptedFile(LogOnEventArgs e)
        {
            using (FilePasswordDialog logOnDialog = new FilePasswordDialog(this, e.EncryptedFileFullName))
            {
                DialogResult dialogResult = logOnDialog.ShowDialog(this);
                if (dialogResult == DialogResult.Retry)
                {
                    e.Passphrase = logOnDialog.ViewModel.Passphrase;
                    e.IsAskingForPreviouslyUnknownPassphrase = true;
                    return;
                }

                if (dialogResult != DialogResult.OK || logOnDialog.ViewModel.Passphrase == Passphrase.Empty)
                {
                    e.Cancel = true;
                    return;
                }
                e.Passphrase = logOnDialog.ViewModel.Passphrase;
            }
            return;
        }

        private void HandleExistingAccountLogOn(LogOnEventArgs e)
        {
            LogOnAccountViewModel viewModel = new LogOnAccountViewModel(Resolve.UserSettings);
            using (LogOnAccountDialog logOnDialog = new LogOnAccountDialog(this, viewModel))
            {
                DialogResult dialogResult = logOnDialog.ShowDialog(this);

                if (dialogResult == DialogResult.Retry)
                {
                    e.UserEmail = String.Empty;
                    e.Passphrase = Passphrase.Empty;
                    New<ICache>().RemoveItem(CacheKey.RootKey);
                    return;
                }

                if (dialogResult == DialogResult.Cancel)
                {
                    StopAndExit();
                }

                if (dialogResult != DialogResult.OK || viewModel.Passphrase.Length == 0)
                {
                    e.Cancel = true;
                    return;
                }

                e.Passphrase = new Passphrase(viewModel.Passphrase);
                e.UserEmail = viewModel.UserEmail;
            }
            return;
        }

        private void AxCryptMainForm_CommandComplete(object sender, CommandCompleteEventArgs e)
        {
            Resolve.UIThread.SendToAsync(async () => await DoRequestAsync(e));
        }

        private CommandCompleteEventArgs _pendingRequest = new CommandCompleteEventArgs(CommandVerb.Unknown, new string[0]);

        private async Task DoRequestAsync(CommandCompleteEventArgs e)
        {
            switch (e.Verb)
            {
                case CommandVerb.About:
                    New<AboutBox>().ShowNow();
                    return;

                case CommandVerb.Exit:
                    StopAndExit();
                    return;
            }

            if (!Resolve.KnownIdentities.IsLoggedOn)
            {
                switch (e.Verb)
                {
                    case CommandVerb.Encrypt:
                    case CommandVerb.Decrypt:
                    case CommandVerb.Open:
                    case CommandVerb.Show:
                    case CommandVerb.ShowLogOn:
                        _pendingRequest = e;
                        await SignInAsync();
                        return;

                    default:
                        break;
                }
            }

            switch (e.Verb)
            {
                case CommandVerb.Encrypt:
                    await _fileOperationViewModel.EncryptFiles.ExecuteAsync(e.Arguments);
                    break;

                case CommandVerb.Decrypt:
                    await _fileOperationViewModel.DecryptFiles.ExecuteAsync(e.Arguments);
                    break;

                case CommandVerb.Open:
                    await _fileOperationViewModel.OpenFiles.ExecuteAsync(e.Arguments);
                    break;

                case CommandVerb.Wipe:
                    await _fileOperationViewModel.WipeFiles.ExecuteAsync(e.Arguments);
                    break;

                case CommandVerb.RandomRename:
                    await PremiumFeatureAction(LicenseCapability.RandomRename, () => _fileOperationViewModel.RandomRenameFiles.ExecuteAsync(e.Arguments));
                    break;

                case CommandVerb.Show:
                    RestoreWindowWithFocus();
                    break;

                case CommandVerb.SignOut:
                    if (New<KnownIdentities>().IsLoggedOn)
                    {
                        New<KnownIdentities>().DefaultEncryptionIdentity = LogOnIdentity.Empty;
                        await SignInAsync();
                    }
                    break;

                case CommandVerb.Register:
                    Process.Start(Texts.LinkToSignUpWebPage.QueryFormat(Resolve.UserSettings.AccountWebUrl));
                    break;

                default:
                    break;
            }
        }

        private void RestoreWindowWithFocus()
        {
            if (ContainsFocus)
            {
                return;
            }

            RestoreFormWithFocus(this);
        }

        private static void RestoreFormWithFocus(Form form)
        {
            do
            {
                form.Show();
                form.WindowState = FormWindowState.Normal;
                form.Activate();
                form.Focus();
                form.BringToFront();

                form = form.OwnedForms.FirstOrDefault();
            } while (form != null);
        }

        private DragDropEffects GetEffectsForMainToolStrip(DragEventArgs e)
        {
            if (_mainViewModel.DragAndDropFiles.Count() != 1)
            {
                return DragDropEffects.None;
            }
            Point point = _mainToolStrip.PointToClient(new Point(e.X, e.Y));
            ToolStripButton button = _mainToolStrip.GetItemAt(point) as ToolStripButton;
            if (button == null)
            {
                return DragDropEffects.None;
            }
            if (!button.Enabled)
            {
                return DragDropEffects.None;
            }
            if (button == _encryptToolStripButton)
            {
                if ((_mainViewModel.DragAndDropFilesTypes & FileInfoTypes.EncryptableFile) == FileInfoTypes.EncryptableFile)
                {
                    return (DragDropEffects.Link | DragDropEffects.Copy) & e.AllowedEffect;
                }
            }
            if (button == _keyShareToolStripButton && _mainViewModel.License.Has(LicenseCapability.KeySharing))
            {
                if ((_mainViewModel.DragAndDropFilesTypes & FileInfoTypes.EncryptedFile) == FileInfoTypes.EncryptedFile)
                {
                    return (DragDropEffects.Link | DragDropEffects.Copy) & e.AllowedEffect;
                }
            }
            return DragDropEffects.None;
        }

        private DragDropEffects GetEffectsForRecentFiles(DragEventArgs e)
        {
            if (!_mainViewModel.DroppableAsRecent && !_mainViewModel.DroppableAsWatchedFolder)
            {
                return DragDropEffects.None;
            }
            return (DragDropEffects.Link | DragDropEffects.Copy) & e.AllowedEffect;
        }

        public DragDropEffects GetEffectsForWatchedFolders(DragEventArgs e)
        {
            if (e == null)
            {
                throw new ArgumentNullException("e");
            }

            if (!_mainViewModel.DroppableAsWatchedFolder)
            {
                return DragDropEffects.None;
            }
            return (DragDropEffects.Link | DragDropEffects.Copy) & e.AllowedEffect;
        }

        private void SetWindowTextWithLogonStatus(bool isLoggedOn)
        {
            string logonStatus;
            if (isLoggedOn)
            {
                UserKeyPair userKeys = Resolve.KnownIdentities.DefaultEncryptionIdentity.UserKeys;
                logonStatus = userKeys != UserKeyPair.Empty ? Texts.AccountLoggedOnStatusText.InvariantFormat(userKeys.UserEmail) : Texts.LoggedOnStatusText;
            }
            else
            {
                logonStatus = Texts.LoggedOffStatusText;
            }
            string text = Texts.TitleWindowSignInStatus.InvariantFormat(_mainViewModel.Title, logonStatus);
            if (New<AxCryptOnlineState>().IsOffline)
            {
                text = $"{text} [{Texts.OfflineIndicatorText}]";
            }
            Text = text;
        }

        private void SetSoftwareStatus()
        {
            VersionUpdateStatus status = _mainViewModel.VersionUpdateStatus;
            switch (status)
            {
                case VersionUpdateStatus.ShortTimeSinceLastSuccessfulCheck:
                case VersionUpdateStatus.IsUpToDate:
                    _softwareStatusButton.ToolTipText = Texts.NoNeedToCheckForUpdatesTooltip;
                    _softwareStatusButton.Image = Resources.bulb_green_40px;
                    break;

                case VersionUpdateStatus.LongTimeSinceLastSuccessfulCheck:
                    _softwareStatusButton.ToolTipText = Texts.OldVersionTooltip;
                    _softwareStatusButton.Image = Resources.bulb_red_40px;
                    break;

                case VersionUpdateStatus.NewerVersionIsAvailable:
                    _softwareStatusButton.ToolTipText = Texts.NewVersionIsAvailableTooltip.InvariantFormat(_mainViewModel.DownloadVersion.Version);
                    _softwareStatusButton.Image = Resources.bulb_red_40px;
                    break;

                case VersionUpdateStatus.Unknown:
                    _softwareStatusButton.ToolTipText = Texts.ClickToCheckForNewerVersionTooltip;
                    _softwareStatusButton.Image = Resources.bulb_red_40px;
                    break;
            }

            string msg = GetCriticalUpdateWarning(_mainViewModel.DownloadVersion.Level);
            if (msg.Length == 0)
            {
                return;
            }

            MessageDialog.ShowOk(this, string.Empty, msg);
            Process.Start(Resolve.UserSettings.UpdateUrl.ToString());
        }

        private static string GetCriticalUpdateWarning(UpdateLevels level)
        {
            if (level.HasFlag(UpdateLevels.Security))
            {
                return Texts.SecurityUpdateAvailableWarning;
            }
            if (level.HasFlag(UpdateLevels.Reliability))
            {
                return Texts.ReliabilityUpdateAvailableWarning;
            }
            return string.Empty;
        }

        private void UpdateDebugMode(bool enabled)
        {
            _optionsDebugToolStripMenuItem.Checked = enabled;
            _debugToolStripMenuItem.Visible = enabled;
        }

        private void UpdateWatchedFolders(IEnumerable<string> watchedFolders)
        {
            _watchedFoldersListView.BeginUpdate();
            try
            {
                _watchedFoldersListView.Items.Clear();
                foreach (string folder in watchedFolders)
                {
                    ListViewItem item = _watchedFoldersListView.Items.Add(folder);
                    item.Name = folder;
                }
            }
            finally
            {
                _watchedFoldersListView.EndUpdate();
            }
        }

        private static async Task<EncryptedProperties> EncryptedPropertiesAsync(IDataStore dataStore)
        {
            return await Task.Run(() => EncryptedProperties.Create(dataStore));
        }

        private void UpdateKnownFolders(IEnumerable<KnownFolder> folders)
        {
            GetKnownFoldersToolItems().Skip(1).ToList().ForEach(f => _mainToolStrip.Items.Remove(f));

            bool anyFolders = folders.Any();
            GetKnownFoldersToolItems().First().Visible = anyFolders;

            if (!anyFolders)
            {
                return;
            }

            int i = _mainToolStrip.Items.IndexOf(GetKnownFoldersToolItems().First()) + 1;
            foreach (KnownFolder knownFolder in folders)
            {
                ToolStripButton button = new ToolStripButton((Image)knownFolder.Image);
                button.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                button.Size = new Size(40, 40);
                button.Margin = new Padding(0);
                button.Padding = new Padding(0);
                button.AutoSize = false;
                button.ImageAlign = ContentAlignment.MiddleCenter;
                button.Tag = knownFolder;
                button.Click += async (sender, e) =>
                {
                    ToolStripItem item = sender as ToolStripItem;
                    await _fileOperationViewModel.OpenFilesFromFolder.ExecuteAsync(((KnownFolder)item.Tag).My.FullName);
                };
                button.Enabled = knownFolder.Enabled;
                _mainToolStrip.Items.Insert(i, button);
                ++i;
            }
        }

        private List<ToolStripItem> GetKnownFoldersToolItems()
        {
            List<ToolStripItem> buttons = new List<ToolStripItem>();
            int i = _mainToolStrip.Items.IndexOf(_knownFoldersSeparator);
            buttons.Add(_mainToolStrip.Items[i++]);
            while (i < _mainToolStrip.Items.Count && _mainToolStrip.Items[i] is ToolStripButton)
            {
                buttons.Add(_mainToolStrip.Items[i++]);
            }
            return buttons;
        }

        public bool CheckStatusAndShowMessage(ErrorStatus status, string displayContext, string message)
        {
            switch (status)
            {
                case ErrorStatus.Success:
                    return true;

                case ErrorStatus.UnspecifiedError:
                    Texts.FileOperationFailed.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.FileAlreadyExists:
                    Texts.FileAlreadyExists.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.FileDoesNotExist:
                    Texts.FileDoesNotExist.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.CannotWriteDestination:
                    Texts.CannotWrite.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.CannotStartApplication:
                    Texts.CannotStartApplication.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.InconsistentState:
                    Texts.InconsistentState.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.InvalidKey:
                    Texts.InvalidKey.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.Canceled:
                    break;

                case ErrorStatus.Exception:
                    string msg = Texts.Exception.InvariantFormat(displayContext);
                    if (!string.IsNullOrEmpty(message))
                    {
                        msg = "{0} [{1}]".InvariantFormat(msg, message);
                    }
                    msg.ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.InvalidPath:
                    Texts.InvalidPath.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.FolderAlreadyWatched:
                    Texts.FolderAlreadyWatched.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.FileLocked:
                    Texts.FileIsLockedWarning.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.FileWriteProtected:
                    Texts.FileIsWriteProtectedWarning.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.WrongFileExtensionError:
                    Texts.WrongFileExtensionWarning.InvariantFormat(displayContext, OS.Current.AxCryptExtension).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.Unknown:
                    Texts.UnknownFileStatus.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.Working:
                    Texts.WorkingFileStatus.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.Aborted:
                    Texts.AbortedFileStatus.InvariantFormat(displayContext).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;

                case ErrorStatus.FileAlreadyEncrypted:
                    Texts.FileAlreadyEncryptedStatus.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                case ErrorStatus.MagicGuidMissing:
                    Texts.MagicGuidMIssingFileStatus.InvariantFormat(displayContext).ShowWarning(this, Texts.WarningTitle);
                    break;

                default:
                    Texts.UnrecognizedError.InvariantFormat(displayContext, status).ShowWarning(this, Texts.MessageUnexpectedErrorTitle);
                    break;
            }
            return false;
        }

        private void _exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShutDownAndExit();
        }

        private void ShutDownAndExit()
        {
            ShutDownBackgroundSafe();

            EncryptPendingFiles();
            WaitForBackgroundToComplete();

            WarnIfAnyDecryptedFiles();

            Application.Exit();
        }

        private void WaitForBackgroundToComplete()
        {
            while (New<IProgressBackground>().Busy)
            {
                Application.DoEvents();
            }
        }

        private void StopAndExit()
        {
            ShutDownBackgroundSafe();

            throw new ApplicationExitException();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void ShutDownBackgroundSafe()
        {
            try
            {
                New<WorkFolderWatcher>().Dispose();
            }
            catch
            {
            }
            try
            {
                New<ActiveFileWatcher>().Dispose();
            }
            catch
            {
            }

            try
            {
                while (New<IProgressBackground>().Busy)
                {
                    Application.DoEvents();
                }
            }
            catch
            {
            }
        }

        #region ToolStrip

        private async void MainToolStrip_DragDrop(object sender, DragEventArgs e)
        {
            Point point = _mainToolStrip.PointToClient(new Point(e.X, e.Y));
            ToolStripButton button = _mainToolStrip.GetItemAt(point) as ToolStripButton;
            if (button == null)
            {
                return;
            }
            if (!button.Enabled)
            {
                return;
            }

            await ((IAsyncAction)button.Tag).ExecuteAsync(_mainViewModel.DragAndDropFiles);
        }

        #endregion ToolStrip

        private void EncryptPendingFiles()
        {
            _mainViewModel.EncryptPendingFiles.Execute(null);
        }

        private void WarnIfAnyDecryptedFiles()
        {
            IEnumerable<ActiveFile> openFiles = _mainViewModel.DecryptedFiles;
            if (!openFiles.Any())
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Texts.DecryptedFilesWarning).AppendLine();
            foreach (ActiveFile openFile in openFiles)
            {
                sb.Append("{0}{1}".InvariantFormat(Path.GetFileName(openFile.DecryptedFileInfo.FullName), Environment.NewLine));
            }
            MessageDialog.ShowOk(this, Texts.WarningTitle, sb.ToString());
        }

        private void SetSortOrder(int column)
        {
            ActiveFileComparer comparer = GetComparer(column, Preferences.RecentFilesSortColumn == column ? Preferences.RecentFilesAscending : false);
            if (comparer == null)
            {
                return;
            }
            Preferences.RecentFilesAscending = !comparer.ReverseSort;
            Preferences.RecentFilesSortColumn = column;
            _mainViewModel.RecentFilesComparer = comparer;
        }

        private static ActiveFileComparer GetComparer(int column, bool reverseSort)
        {
            ActiveFileComparer comparer;
            switch (column)
            {
                case 0:
                    comparer = ActiveFileComparer.DecryptedNameComparer;
                    break;

                case 1:
                    comparer = ActiveFileComparer.DateComparer;
                    break;

                case 2:
                    comparer = ActiveFileComparer.EncryptedNameComparer;
                    break;

                case 3:
                    comparer = ActiveFileComparer.CryptoNameComparer;
                    break;

                default:
                    throw new ArgumentException("column is wrong.");
            }
            comparer.ReverseSort = reverseSort;
            return comparer;
        }

        private static ActiveFileComparer ChooseComparer(ActiveFileComparer current, ActiveFileComparer comparer)
        {
            if (current != null && current.GetType() == comparer.GetType())
            {
                comparer.ReverseSort = !current.ReverseSort;
            }
            return comparer;
        }

        private async Task PremiumFeature_ClickAsync(LicenseCapability requiredCapability, Func<object, EventArgs, Task> realHandler, object sender, EventArgs e)
        {
            if (_mainViewModel.License.Has(requiredCapability))
            {
                await realHandler(sender, e);
                return;
            }

            Process.Start(Texts.LinkToAxCryptPremiumPurchasePage.QueryFormat(Resolve.UserSettings.AccountWebUrl, Resolve.KnownIdentities.DefaultEncryptionIdentity.UserEmail));
        }

        private Task PremiumFeatureAction(LicenseCapability requiredCapability, Func<Task> realHandler)
        {
            if (_mainViewModel.License.Has(requiredCapability))
            {
                return realHandler();
            }

            return MessageDialog.ShowOkAsync(this, Texts.WarningTitle, Texts.PremiumFeatureToolTipText);
        }

        private void CloseAndRemoveOpenFilesToolStripButton_Click(object sender, EventArgs e)
        {
            EncryptPendingFiles();
        }

        private void ProgressBackgroundWorker_ProgressBarClicked(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            _progressContextMenuStrip.Tag = sender;
            _progressContextMenuStrip.Show((Control)sender, e.Location);
        }

        private void ProgressBackgroundWorker_ProgressBarCreated(object sender, ControlEventArgs e)
        {
            _progressTableLayoutPanel.Controls.Add(e.Control);
        }

        private void ProgressContextCancelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            ContextMenuStrip menuStrip = (ContextMenuStrip)menuItem.GetCurrentParent();
            ProgressBar progressBar = (ProgressBar)menuStrip.Tag;
            IProgressContext progress = (IProgressContext)progressBar.Tag;
            progress.Cancel = true;
        }

        private void LanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            SetLanguage((string)menuItem.Tag);
        }

        private void SetLanguage(string cultureName)
        {
            Resolve.UserSettings.CultureName = cultureName;
            if (Resolve.Log.IsInfoEnabled)
            {
                Resolve.Log.LogInfo("Set new UI language culture to '{0}'.".InvariantFormat(Resolve.UserSettings.CultureName));
            }

            InitializeContentResources();
            SetWindowTextWithLogonStatus(_mainViewModel.LoggedOn);
            SetDaysLeftWarning();
            SetSoftwareStatus();
        }

        private void OptionsLanguageToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem languageMenu = (ToolStripMenuItem)sender;
            string currentLanguage = Thread.CurrentThread.CurrentUICulture.Name;
            foreach (ToolStripItem item in languageMenu.DropDownItems)
            {
                string languageName = item.Tag as string;
                ((ToolStripMenuItem)item).Checked = languageName == currentLanguage;
            }
        }

        private void _softwareStatusButton_Click(object sender, EventArgs e)
        {
            switch (_mainViewModel.VersionUpdateStatus)
            {
                case VersionUpdateStatus.LongTimeSinceLastSuccessfulCheck:
                case VersionUpdateStatus.ShortTimeSinceLastSuccessfulCheck:
                case VersionUpdateStatus.IsUpToDate:
                case VersionUpdateStatus.Unknown:
                    _mainViewModel.AxCryptUpdateCheck.Execute(DateTime.MinValue);
                    break;

                case VersionUpdateStatus.NewerVersionIsAvailable:
                    Process.Start(Resolve.UserSettings.UpdateUrl.ToString());
                    break;

                default:
                    break;
            }
        }

        private void SetOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (DebugOptionsDialog dialog = new DebugOptionsDialog(this))
            {
                dialog._restApiBaseUrl.Text = Resolve.UserSettings.RestApiBaseUrl.ToString();
                dialog._timeoutTimeSpan.Text = Resolve.UserSettings.ApiTimeout.ToString();
                DialogResult result = dialog.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return;
                }
                Resolve.UserSettings.RestApiBaseUrl = new Uri(dialog._restApiBaseUrl.Text);
                Resolve.UserSettings.ApiTimeout = TimeSpan.Parse(dialog._timeoutTimeSpan.Text);
            }
        }

        private void _aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            New<AboutBox>().ShowNow();
        }

        private void HelpToolStripButton_Click(object sender, EventArgs e)
        {
            Process.Start(Resolve.UserSettings.AxCrypt2HelpUrl.ToString());
        }

        private void _viewHelpMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Resolve.UserSettings.AxCrypt2HelpUrl.ToString());
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeInternal();
            }
            base.Dispose(disposing);
        }

        private void DisposeInternal()
        {
            if (components != null)
            {
                components.Dispose();
            }
            if (_mainViewModel != null)
            {
                _mainViewModel.Dispose();
                _mainViewModel = null;
            }
        }

        private void ClearPassphraseMemoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearAllSettingsAndReinitialize();
            StopAndExit();
        }

        private void ClearAllSettingsAndReinitialize()
        {
            ShutDownBackgroundSafe();

            Resolve.UserSettings.Delete();
            Resolve.FileSystemState.Delete();
            Resolve.WorkFolder.FileInfo.FileItemInfo(LocalAccountService.FileName).Delete();
            New<KnownPublicKeys>().Delete();
            Resolve.UserSettings.SettingsVersion = Resolve.UserSettings.CurrentSettingsVersion;
        }

        private void PolicyMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            SetCheckedToolStripMenuItem(item);
            ReRegisterPolicy(item);
            _mainViewModel.LicenseUpdate.Execute(null);
        }

        private static void ReRegisterPolicy(ToolStripMenuItem item)
        {
            if (item.Text == Texts.LicenseFreeNameText)
            {
                TypeMap.Register.New<LicensePolicy>(() => new FreeForcedLicensePolicy());
                return;
            }
            if (item.Text == Texts.LicensePremiumNameText)
            {
                TypeMap.Register.New<LicensePolicy>(() => new PremiumForcedLicensePolicy());
                return;
            }
            throw new InvalidOperationException("Unexpected license policy name.");
        }

        private static void SetCheckedToolStripMenuItem(ToolStripMenuItem item)
        {
            foreach (ToolStripItem tsi in item.GetCurrentParent().Items)
            {
                ((ToolStripMenuItem)tsi).Checked = false;
            }
            item.Checked = true;
        }

        private void CryptoPolicyToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
        }

        private void loggingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            item.Checked = !item.Checked;
            if (_debugOutput == null)
            {
                _debugOutput = new DebugLogOutputDialog();
            }
            _debugOutput.Visible = item.Checked;
        }

        private void encryptedFoldersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowWatchedFolders(new string[0]);
        }

        private void ShowWatchedFolders(IEnumerable<string> additional)
        {
            using (WatchedFoldersDialog dialog = new WatchedFoldersDialog(this, additional))
            {
                dialog.ShowDialog();
            }
        }

        private void CreateAccountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateNewAccountDialog dialog = new CreateNewAccountDialog(this, String.Empty, EmailAddress.Empty))
            {
                dialog.ShowDialog();
            }
        }

        private void ManageAccountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ManageAccountDialog dialog = new ManageAccountDialog(this, Resolve.UserSettings))
            {
                dialog.ShowDialog();
            }
        }

        private async void ChangePassphraseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AccountStorage accountStorage = new AccountStorage(New<LogOnIdentity, IAccountService>(Resolve.KnownIdentities.DefaultEncryptionIdentity));
            ManageAccountViewModel viewModel = new ManageAccountViewModel(accountStorage);

            string passphrase;
            using (NewPassphraseDialog dialog = new NewPassphraseDialog(this, Texts.ChangePassphraseDialogTitle, String.Empty, String.Empty))
            {
                dialog.ShowPassphraseCheckBox.Checked = Resolve.UserSettings.DisplayEncryptPassphrase;
                DialogResult dialogResult = dialog.ShowDialog(this);
                if (dialogResult != DialogResult.OK || dialog.PassphraseTextBox.Text.Length == 0)
                {
                    return;
                }
                Resolve.UserSettings.DisplayEncryptPassphrase = dialog.ShowPassphraseCheckBox.Checked;
                passphrase = dialog.PassphraseTextBox.Text;
            }
            await viewModel.ChangePassphraseAsync.ExecuteAsync(passphrase);
            await LogOnOrLogOffAndLogOnAgainAsync();
        }

        private void ExportMySharingKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UserKeyPair keyPair = Resolve.KnownIdentities.DefaultEncryptionIdentity.UserKeys;
            EmailAddress userEmail = keyPair.UserEmail;
            IAsymmetricPublicKey publicKey = keyPair.KeyPair.PublicKey;
            string fileName;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = Texts.DialogExportSharingKeyTitle;
                sfd.DefaultExt = ".txt";
                sfd.AddExtension = true;
                sfd.Filter = Texts.DialogExportSharingKeyFilter;
                sfd.CheckPathExists = true;
                sfd.FileName = Texts.DialogExportSharingKeyFileName.InvariantFormat(userEmail.Address, publicKey.Tag);
                sfd.ValidateNames = true;
                sfd.OverwritePrompt = true;
                sfd.RestoreDirectory = false;
                DialogResult saveAsResult = sfd.ShowDialog();
                if (saveAsResult != DialogResult.OK)
                {
                    return;
                }
                fileName = sfd.FileName;
            }

            UserPublicKey userPublicKey = new UserPublicKey(userEmail, publicKey);
            string serialized = Resolve.Serializer.Serialize(userPublicKey);
            File.WriteAllText(fileName, serialized);
        }

        private void ImportOthersSharingKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileSelectionViewModel fileSelection = new FileSelectionViewModel();
            fileSelection.SelectingFiles += (sfsender, sfe) => { New<IDataItemSelection>().HandleSelection(sfe); };
            fileSelection.SelectFiles.Execute(FileSelectionType.ImportPublicKeys);

            ImportPublicKeysViewModel importPublicKeys = new ImportPublicKeysViewModel(New<KnownPublicKeys>);
            importPublicKeys.ImportFiles.Execute(fileSelection.SelectedFiles);
        }

        private void ImportMyPrivateKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ImportPrivatePasswordDialog dialog = new ImportPrivatePasswordDialog(this, Resolve.UserSettings, Resolve.KnownIdentities))
            {
                dialog.ShowDialog();
            }
        }

        private async Task ShareKeysAsync(IEnumerable<string> fileNames)
        {
            IEnumerable<Tuple<string, EncryptedProperties>> files = await ListValidAsync(fileNames);
            IEnumerable<UserPublicKey> sharedWith = files.SelectMany(f => f.Item2.SharedKeyHolders).Distinct();
            SharingListViewModel viewModel = new SharingListViewModel(sharedWith, Resolve.KnownIdentities.DefaultEncryptionIdentity);
            using (KeyShareDialog dialog = new KeyShareDialog(this, viewModel))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                sharedWith = dialog.SharedWith;
            }

            using (KnownPublicKeys knowPublicKeys = New<KnownPublicKeys>())
            {
                sharedWith = New<KnownPublicKeys>().PublicKeys.Where(pk => sharedWith.Any(s => s.Email == pk.Email)).ToList();
            }
            EncryptionParameters encryptionParameters = new EncryptionParameters(Resolve.CryptoFactory.Default(New<ICryptoPolicy>()).CryptoId, Resolve.KnownIdentities.DefaultEncryptionIdentity);
            encryptionParameters.Add(sharedWith);

            await ChangeEncryptionAsync(files.Select(f => f.Item1), encryptionParameters);
        }

        private async Task ChangeEncryptionAsync(IEnumerable<string> files, EncryptionParameters encryptionParameters)
        {
            await Resolve.ParallelFileOperation.DoFilesAsync(files, (string file, IProgressContext progress) =>
            {
                New<AxCryptFile>().ChangeEncryption(New<IDataStore>(file), Resolve.KnownIdentities.DefaultEncryptionIdentity, encryptionParameters, progress);
                return Task.FromResult(new FileOperationContext(file, ErrorStatus.Success));
            },
            (FileOperationContext foc) =>
            {
                if (foc.ErrorStatus == ErrorStatus.Success)
                {
                    Resolve.SessionNotify.Notify(new SessionNotification(SessionNotificationType.ActiveFileChange, foc.FullName));
                }
                CheckStatusAndShowMessage(foc.ErrorStatus, foc.FullName, foc.InternalMessage);
            });
        }

        private async Task<IEnumerable<Tuple<string, EncryptedProperties>>> ListValidAsync(IEnumerable<string> fileNames)
        {
            List<Tuple<string, EncryptedProperties>> files = new List<Tuple<string, EncryptedProperties>>();
            foreach (string file in fileNames)
            {
                EncryptedProperties properties = await EncryptedPropertiesAsync(New<IDataStore>(file));
                if (properties.IsValid)
                {
                    files.Add(new Tuple<string, EncryptedProperties>(file, properties));
                }
            }

            return files;
        }

        private async Task WatchedFoldersKeySharingAsync(IEnumerable<string> folderPaths)
        {
            if (!folderPaths.Any())
            {
                return;
            }

            IEnumerable<EmailAddress> sharedWithEmailAddresses = folderPaths.ToWatchedFolders().SharedWith();

            IEnumerable<UserPublicKey> sharedWithPublicKeys;
            using (KnownPublicKeys knownPublicKeys = New<KnownPublicKeys>())
            {
                sharedWithPublicKeys = knownPublicKeys.PublicKeys.Where(pk => sharedWithEmailAddresses.Any(s => s == pk.Email)).ToList();
            }

            SharingListViewModel viewModel = new SharingListViewModel(sharedWithPublicKeys, Resolve.KnownIdentities.DefaultEncryptionIdentity);
            using (KeyShareDialog dialog = new KeyShareDialog(this, viewModel))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                sharedWithPublicKeys = dialog.SharedWith;
            }

            await Task.Run(async () =>
            {
                foreach (WatchedFolder watchedFolder in folderPaths.ToWatchedFolders())
                {
                    WatchedFolder wf = new WatchedFolder(watchedFolder, sharedWithPublicKeys);
                    Resolve.FileSystemState.AddWatchedFolder(wf);
                }
                IEnumerable<string> files = folderPaths.SelectMany(fp => New<IDataContainer>(fp).Files.Select(ds => ds.FullName));
                EncryptionParameters encryptionParameters = new EncryptionParameters(Resolve.CryptoFactory.Default(New<ICryptoPolicy>()).CryptoId, Resolve.KnownIdentities.DefaultEncryptionIdentity, sharedWithPublicKeys.Select(pk => pk.Email));

                await ChangeEncryptionAsync(files, encryptionParameters);
            });
        }

        private void ExportMyPrivateKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UserKeyPair userKeyPair = Resolve.KnownIdentities.DefaultEncryptionIdentity.UserKeys;
            IAsymmetricPublicKey publicKey = userKeyPair.KeyPair.PublicKey;

            string fileName;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = Texts.DialogExportAxCryptIdTitle;
                sfd.DefaultExt = ".axx";
                sfd.AddExtension = true;
                sfd.Filter = Texts.DialogExportAxCryptIdFilter;
                sfd.CheckPathExists = true;
                sfd.FileName = Texts.DialogExportAxCryptIdFileName.InvariantFormat(userKeyPair.UserEmail, publicKey.Tag);
                sfd.ValidateNames = true;
                sfd.OverwritePrompt = true;
                sfd.RestoreDirectory = false;
                DialogResult saveAsResult = sfd.ShowDialog();
                if (saveAsResult != DialogResult.OK)
                {
                    return;
                }
                fileName = sfd.FileName;
            }

            byte[] export = userKeyPair.ToArray(Resolve.KnownIdentities.DefaultEncryptionIdentity.Passphrase);
            File.WriteAllBytes(fileName, export);
        }

        private void tryBrokenFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _mainViewModel.TryBrokenFile = !_mainViewModel.TryBrokenFile;
        }

        private void AxCryptMainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                return;
            }

            if (_debugOutput != null)
            {
                _debugOutput.AllowClose = true;
            }

            WireDownEvents();
        }

        private void WatchedFoldersAddSecureFolderMenuItem_Click(object sender, EventArgs e)
        {
            string folder = SelectSecureFolder();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            _mainViewModel.AddWatchedFolders.Execute(new string[] { folder });
        }

        private string SelectSecureFolder()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = Texts.AddSecureFolderTitle;
                fbd.ShowNewFolderButton = true;
                DialogResult result = fbd.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    return fbd.SelectedPath;
                }
            }
            return String.Empty;
        }
    }
}