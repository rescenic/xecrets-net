﻿#region Coypright and License

/*
 * AxCrypt - Copyright 2014, Svante Seleborg, All Rights Reserved
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
 * http://www.axantum.com for more information about the author.
*/

#endregion Coypright and License

using Axantum.AxCrypt.Abstractions;
using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.Extensions;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Runtime;
using Axantum.AxCrypt.Core.Session;
using System;
using System.Collections.Generic;
using System.Linq;

using static Axantum.AxCrypt.Abstractions.TypeResolve;

namespace Axantum.AxCrypt.Core.UI.ViewModel
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private FileSystemState _fileSystemState;

        private IUserSettings _userSettings;

        private UpdateCheck _updateCheck;

        public bool LoggedOn { get { return GetProperty<bool>(nameof(LoggedOn)); } set { SetProperty(nameof(LoggedOn), value); } }

        public bool EncryptFileEnabled { get { return GetProperty<bool>(nameof(EncryptFileEnabled)); } set { SetProperty(nameof(EncryptFileEnabled), value); } }

        public bool DecryptFileEnabled { get { return GetProperty<bool>(nameof(DecryptFileEnabled)); } set { SetProperty(nameof(DecryptFileEnabled), value); } }

        public bool OpenEncryptedEnabled { get { return GetProperty<bool>(nameof(OpenEncryptedEnabled)); } set { SetProperty(nameof(OpenEncryptedEnabled), value); } }

        public bool WatchedFoldersEnabled { get { return GetProperty<bool>(nameof(WatchedFoldersEnabled)); } set { SetProperty(nameof(WatchedFoldersEnabled), value); } }

        public string Title { get { return GetProperty<string>(nameof(Title)); } set { SetProperty(nameof(Title), value); } }

        public LogOnIdentity Identity { get { return GetProperty<LogOnIdentity>(nameof(Identity)); } set { SetProperty(nameof(Identity), value); } }

        public IEnumerable<string> WatchedFolders { get { return GetProperty<IEnumerable<string>>(nameof(WatchedFolders)); } set { SetProperty(nameof(WatchedFolders), value.ToList()); } }

        public IEnumerable<ActiveFile> RecentFiles { get { return GetProperty<IEnumerable<ActiveFile>>(nameof(RecentFiles)); } set { SetProperty(nameof(RecentFiles), value.ToList()); } }

        public IEnumerable<ActiveFile> DecryptedFiles { get { return GetProperty<IEnumerable<ActiveFile>>(nameof(DecryptedFiles)); } set { SetProperty(nameof(DecryptedFiles), value.ToList()); } }

        public ActiveFileComparer RecentFilesComparer { get { return GetProperty<ActiveFileComparer>(nameof(RecentFilesComparer)); } set { SetProperty(nameof(RecentFilesComparer), value); } }

        public IEnumerable<string> SelectedWatchedFolders { get { return GetProperty<IEnumerable<string>>(nameof(SelectedWatchedFolders)); } set { SetProperty(nameof(SelectedWatchedFolders), value.ToList()); } }

        public IEnumerable<string> SelectedRecentFiles { get { return GetProperty<IEnumerable<string>>(nameof(SelectedRecentFiles)); } set { SetProperty(nameof(SelectedRecentFiles), value.ToList()); } }

        public IEnumerable<string> DragAndDropFiles { get { return GetProperty<IEnumerable<string>>(nameof(DragAndDropFiles)); } set { SetProperty(nameof(DragAndDropFiles), value.ToList()); } }

        public FileInfoTypes DragAndDropFilesTypes { get { return GetProperty<FileInfoTypes>(nameof(DragAndDropFilesTypes)); } set { SetProperty(nameof(DragAndDropFilesTypes), value); } }

        public bool DroppableAsRecent { get { return GetProperty<bool>(nameof(DroppableAsRecent)); } set { SetProperty(nameof(DroppableAsRecent), value); } }

        public bool DroppableAsWatchedFolder { get { return GetProperty<bool>(nameof(DroppableAsWatchedFolder)); } set { SetProperty(nameof(DroppableAsWatchedFolder), value); } }

        public bool FilesArePending { get { return GetProperty<bool>(nameof(FilesArePending)); } set { SetProperty(nameof(FilesArePending), value); } }

        public Version CurrentVersion { get { return GetProperty<Version>(nameof(CurrentVersion)); } set { SetProperty(nameof(CurrentVersion), value); } }

        public Version UpdatedVersion { get { return GetProperty<Version>(nameof(UpdatedVersion)); } set { SetProperty(nameof(UpdatedVersion), value); } }

        public VersionUpdateStatus VersionUpdateStatus { get { return GetProperty<VersionUpdateStatus>(nameof(VersionUpdateStatus)); } set { SetProperty(nameof(VersionUpdateStatus), value); } }

        public bool DebugMode { get { return GetProperty<bool>(nameof(DebugMode)); } set { SetProperty(nameof(DebugMode), value); } }

        public bool TryBrokenFile { get { return GetProperty<bool>(nameof(TryBrokenFile)); } set { SetProperty(nameof(TryBrokenFile), value); } }

        public bool Working { get { return GetProperty<bool>(nameof(Working)); } set { SetProperty(nameof(Working), value); } }

        public IAction RemoveRecentFiles { get; private set; }

        public IAction AddWatchedFolders { get; private set; }

        public IAction EncryptPendingFiles { get; private set; }

        public IAction ClearPassphraseMemory { get; private set; }

        public IAction RemoveWatchedFolders { get; private set; }

        public IAction OpenSelectedFolder { get; private set; }

        public IAction UpdateCheck { get; private set; }

        public MainViewModel(FileSystemState fileSystemState, IUserSettings userSettings)
        {
            _fileSystemState = fileSystemState;
            _userSettings = userSettings;

            InitializePropertyValues();
            BindPropertyChangedEvents();
            SubscribeToModelEvents();
        }

        private void InitializePropertyValues()
        {
            WatchedFoldersEnabled = false;
            WatchedFolders = new string[0];
            DragAndDropFiles = new string[0];
            RecentFiles = new ActiveFile[0];
            SelectedRecentFiles = new string[0];
            SelectedWatchedFolders = new string[0];
            DebugMode = _userSettings.DebugMode;
            TryBrokenFile = _userSettings.TryBrokenFile;
            Title = String.Empty;
            VersionUpdateStatus = UI.VersionUpdateStatus.Unknown;

            AddWatchedFolders = new DelegateAction<IEnumerable<string>>((folders) => AddWatchedFoldersAction(folders), (folders) => LoggedOn);
            RemoveRecentFiles = new DelegateAction<IEnumerable<string>>((files) => RemoveRecentFilesAction(files));
            EncryptPendingFiles = new DelegateAction<object>((parameter) => EncryptPendingFilesAction());
            ClearPassphraseMemory = new DelegateAction<object>((parameter) => ClearPassphraseMemoryAction());
            RemoveWatchedFolders = new DelegateAction<IEnumerable<string>>((folders) => RemoveWatchedFoldersAction(folders), (folders) => LoggedOn);
            OpenSelectedFolder = new DelegateAction<string>((folder) => OpenSelectedFolderAction(folder));
            UpdateCheck = new DelegateAction<DateTime>((utc) => UpdateCheckAction(utc), (utc) => _updateCheck != null);

            DecryptFileEnabled = true;
            OpenEncryptedEnabled = true;
        }

        private void BindPropertyChangedEvents()
        {
            BindPropertyChangedInternal(nameof(DragAndDropFiles), (IEnumerable<string> files) => { DragAndDropFilesTypes = DetermineFileTypes(files.Select(f => New<IDataItem>(f))); });
            BindPropertyChangedInternal(nameof(DragAndDropFiles), (IEnumerable<string> files) => { DroppableAsRecent = DetermineDroppableAsRecent(files.Select(f => New<IDataItem>(f))); });
            BindPropertyChangedInternal(nameof(DragAndDropFiles), (IEnumerable<string> files) => { DroppableAsWatchedFolder = DetermineDroppableAsWatchedFolder(files.Select(f => New<IDataItem>(f))); });
            BindPropertyChangedInternal(nameof(CurrentVersion), (Version cv) => { if (cv != null) UpdateUpdateCheck(cv); });
            BindPropertyChangedInternal(nameof(DebugMode), (bool enabled) => { UpdateDebugMode(enabled); });
            BindPropertyChangedInternal(nameof(TryBrokenFile), (bool enabled) => { _userSettings.TryBrokenFile = enabled; });
            BindPropertyChangedInternal(nameof(RecentFilesComparer), (ActiveFileComparer comparer) => { SetRecentFiles(); });
        }

        private void SubscribeToModelEvents()
        {
            Resolve.SessionNotify.Notification += HandleSessionChanged;
            Resolve.ProgressBackground.WorkStatusChanged += (sender, e) =>
                {
                    Working = Resolve.ProgressBackground.Busy;
                };
            _fileSystemState.ActiveFileChanged += HandleActiveFileChangedEvent;
        }

        private void UpdateDebugMode(bool enabled)
        {
            Resolve.Log.SetLevel(enabled ? LogLevel.Debug : LogLevel.Error);
            OS.Current.DebugMode(enabled);
            _userSettings.DebugMode = enabled;
        }

        private void UpdateUpdateCheck(Version currentVersion)
        {
            DisposeUpdateCheck();
            _updateCheck = New<Version, UpdateCheck>(currentVersion);
            _updateCheck.VersionUpdate += Handle_VersionUpdate;
            UpdateCheckAction(_userSettings.LastUpdateCheckUtc);
        }

        private void Handle_VersionUpdate(object sender, VersionEventArgs e)
        {
            _userSettings.LastUpdateCheckUtc = OS.Current.UtcNow;
            _userSettings.NewestKnownVersion = e.Version.ToString();
            _userSettings.UpdateUrl = e.UpdateWebpageUrl;

            UpdatedVersion = e.Version;
            VersionUpdateStatus = e.VersionUpdateStatus;
        }

        private void HandleActiveFileChangedEvent(object sender, ActiveFileChangedEventArgs e)
        {
            SetFilesArePending();
            SetRecentFiles();
        }

        private static FileInfoTypes DetermineFileTypes(IEnumerable<IDataItem> files)
        {
            FileInfoTypes types = FileInfoTypes.None;
            FileInfoTypes typesToLookFor = FileInfoTypes.EncryptedFile | FileInfoTypes.EncryptableFile;
            foreach (IDataItem file in files)
            {
                types |= file.Type() & typesToLookFor;
                if ((types & typesToLookFor) == typesToLookFor)
                {
                    return types;
                }
            }
            return types;
        }

        private static bool DetermineDroppableAsRecent(IEnumerable<IDataItem> files)
        {
            return files.Any(fileInfo => fileInfo.Type() == FileInfoTypes.EncryptedFile || (Resolve.KnownIdentities.IsLoggedOn && fileInfo.Type() == FileInfoTypes.EncryptableFile));
        }

        private static bool DetermineDroppableAsWatchedFolder(IEnumerable<IDataItem> files)
        {
            if (files.Count() != 1)
            {
                return false;
            }

            IDataItem fileInfo = files.First();
            if (!fileInfo.IsAvailable)
            {
                return false;
            }

            if (!fileInfo.IsFolder)
            {
                return false;
            }

            if (!fileInfo.IsEncryptable())
            {
                return false;
            }

            return true;
        }

        private void HandleSessionChanged(object sender, SessionNotificationEventArgs e)
        {
            switch (e.Notification.NotificationType)
            {
                case SessionNotificationType.WatchedFolderAdded:
                    SetWatchedFolders();
                    break;

                case SessionNotificationType.WatchedFolderRemoved:
                    SetWatchedFolders();
                    break;

                case SessionNotificationType.LogOn:
                    SetLogOnState(Resolve.KnownIdentities.IsLoggedOn);
                    SetWatchedFolders();
                    break;

                case SessionNotificationType.LogOff:
                    SetLogOnState(Resolve.KnownIdentities.IsLoggedOn);
                    SetWatchedFolders();
                    break;

                case SessionNotificationType.WatchedFolderChange:
                    SetFilesArePending();
                    break;

                case SessionNotificationType.KnownKeyChange:
                    if (e.Notification.Identity == LogOnIdentity.Empty)
                    {
                        throw new InvalidOperationException("Attempt to add the empty identity as a known key.");
                    }
                    if (!_fileSystemState.KnownPassphrases.Any(p => p.Thumbprint == e.Notification.Identity.Passphrase.Thumbprint))
                    {
                        _fileSystemState.KnownPassphrases.Add(e.Notification.Identity.Passphrase);
                        _fileSystemState.Save();
                    }
                    break;

                case SessionNotificationType.ActiveFileChange:
                    SetRecentFiles();
                    break;

                case SessionNotificationType.WorkFolderChange:
                case SessionNotificationType.ProcessExit:
                case SessionNotificationType.SessionChange:
                case SessionNotificationType.SessionStart:
                default:
                    break;
            }
        }

        private void SetWatchedFolders()
        {
            WatchedFolders = Resolve.KnownIdentities.LoggedOnWatchedFolders.Select(wf => wf.Path).ToList();
        }

        private void SetRecentFiles()
        {
            List<ActiveFile> activeFiles = new List<ActiveFile>(_fileSystemState.ActiveFiles).ToList();
            if (RecentFilesComparer != null)
            {
                activeFiles.Sort(RecentFilesComparer);
            }
            RecentFiles = activeFiles;
            DecryptedFiles = _fileSystemState.DecryptedActiveFiles;
        }

        private void SetFilesArePending()
        {
            IList<ActiveFile> openFiles = _fileSystemState.DecryptedActiveFiles;
            FilesArePending = openFiles.Count > 0 || Resolve.KnownIdentities.LoggedOnWatchedFolders.SelectMany(wf => New<IDataContainer>(wf.Path).ListEncryptable()).Any();
        }

        private void SetLogOnState(bool isLoggedOn)
        {
            Identity = GetLogOnIdentity(isLoggedOn);
            LoggedOn = isLoggedOn;
            EncryptFileEnabled = isLoggedOn;
            WatchedFoldersEnabled = isLoggedOn;
        }

        private static LogOnIdentity GetLogOnIdentity(bool isLoggedOn)
        {
            if (!isLoggedOn)
            {
                return null;
            }
            return Resolve.KnownIdentities.DefaultEncryptionIdentity;
        }

        private void ClearPassphraseMemoryAction()
        {
            IDataStore fileSystemStateInfo = Resolve.FileSystemState.PathInfo;
            New<AxCryptFile>().Wipe(fileSystemStateInfo, new ProgressContext());
            TypeMap.Register.Singleton<FileSystemState>(() => FileSystemState.Create(fileSystemStateInfo));
            TypeMap.Register.Singleton<KnownIdentities>(() => new KnownIdentities(_fileSystemState, Resolve.SessionNotify));
            Resolve.SessionNotify.Notify(new SessionNotification(SessionNotificationType.SessionStart));
        }

        private static void EncryptPendingFilesAction()
        {
            Resolve.SessionNotify.Notify(new SessionNotification(SessionNotificationType.EncryptPendingFiles));
        }

        private void RemoveRecentFilesAction(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                ActiveFile activeFile = _fileSystemState.FindActiveFileFromEncryptedPath(file);
                if (activeFile != null)
                {
                    _fileSystemState.RemoveActiveFile(activeFile);
                }
            }
            _fileSystemState.Save();
        }

        private void AddWatchedFoldersAction(IEnumerable<string> folders)
        {
            if (!folders.Any())
            {
                return;
            }
            foreach (string folder in folders)
            {
                _fileSystemState.AddWatchedFolder(new WatchedFolder(folder, Resolve.KnownIdentities.DefaultEncryptionIdentity.Tag));
            }
            _fileSystemState.Save();
        }

        private void RemoveWatchedFoldersAction(IEnumerable<string> folders)
        {
            if (!folders.Any())
            {
                return;
            }
            foreach (string watchedFolderPath in folders)
            {
                _fileSystemState.RemoveWatchedFolder(New<IDataContainer>(watchedFolderPath));
            }
            _fileSystemState.Save();
        }

        private static void OpenSelectedFolderAction(string folder)
        {
            New<ILauncher>().Launch(folder);
        }

        private void UpdateCheckAction(DateTime lastUpdateCheckUtc)
        {
            _updateCheck.CheckInBackground(lastUpdateCheckUtc, _userSettings.NewestKnownVersion, _userSettings.UpdateUrl);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeInternal();
            }
        }

        private void DisposeInternal()
        {
            DisposeUpdateCheck();
        }

        private void DisposeUpdateCheck()
        {
            if (_updateCheck != null)
            {
                _updateCheck.VersionUpdate -= Handle_VersionUpdate;
                _updateCheck.Dispose();
            }
        }
    }
}