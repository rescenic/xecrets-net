﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using Axantum.AxCrypt.Core;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.UI;

namespace Axantum.AxCrypt
{
    internal class ActiveFileMonitor
    {
        private string _fileSystemStateFullName;

        private FileSystemWatcher _temporaryDirectoryWatcher;

        private ProgressManager _progressManager;

        public ActiveFileMonitor(ProgressManager progressManager)
        {
            _progressManager = progressManager;
            _fileSystemStateFullName = Path.Combine(TemporaryDirectoryInfo.FullName, "FileSystemState.xml");

            _temporaryDirectoryWatcher = new FileSystemWatcher(TemporaryDirectoryInfo.FullName);
            _temporaryDirectoryWatcher.Changed += TemporaryDirectoryWatcher_Changed;
            _temporaryDirectoryWatcher.Created += TemporaryDirectoryWatcher_Changed;
            _temporaryDirectoryWatcher.Deleted += TemporaryDirectoryWatcher_Changed;
            _temporaryDirectoryWatcher.IncludeSubdirectories = true;
            _temporaryDirectoryWatcher.NotifyFilter = NotifyFilters.LastWrite;

            FileSystemState.Load(AxCryptEnvironment.Current.FileInfo(_fileSystemStateFullName));
        }

        public event EventHandler<EventArgs> Changed;

        public void AddActiveFile(ActiveFile activeFile)
        {
            FileSystemState.Current.Add(activeFile);
            FileSystemState.Current.Save();
            OnChanged(new EventArgs());
        }

        private bool _ignoreApplication;

        public bool IgnoreApplication
        {
            get
            {
                return _ignoreApplication;
            }
            set
            {
                _ignoreApplication = value;
            }
        }

        private void OnChanged(EventArgs eventArgs)
        {
            EventHandler<EventArgs> changed = Changed;
            if (changed != null)
            {
                changed(null, eventArgs);
            }
        }

        public void ForEach(bool forceChange, Func<ActiveFile, ActiveFile> action)
        {
            bool isChanged = forceChange;
            List<ActiveFile> activeFiles = new List<ActiveFile>();
            foreach (ActiveFile activeFile in FileSystemState.Current.ActiveFiles)
            {
                ActiveFile updatedActiveFile = action(activeFile);
                activeFiles.Add(updatedActiveFile);
                if (updatedActiveFile != activeFile)
                {
                    activeFile.Dispose();
                }
                isChanged |= updatedActiveFile != activeFile;
            }
            if (isChanged)
            {
                FileSystemState.Current.ActiveFiles = activeFiles;
                FileSystemState.Current.Save();
                OnChanged(new EventArgs());
            }
        }

        public void CheckActiveFilesStatus()
        {
            CheckActiveFilesStatusInternal(false);
        }

        public void ForceActiveFilesStatus()
        {
            CheckActiveFilesStatusInternal(true);
        }

        private void CheckActiveFilesStatusInternal(bool forceChanged)
        {
            ForEach(forceChanged, (ActiveFile activeFile) =>
            {
                if (DateTime.UtcNow - activeFile.LastAccessTimeUtc > new TimeSpan(0, 0, 5))
                {
                    activeFile = CheckIfCreated(activeFile);
                    activeFile = CheckIfProcessExited(activeFile);
                    activeFile = CheckIfTimeToUpdate(activeFile);
                    activeFile = CheckIfTimeToDelete(activeFile);
                }
                return activeFile;
            });
        }

        private static ActiveFile CheckIfCreated(ActiveFile activeFile)
        {
            if (activeFile.Status == ActiveFileStatus.NotDecrypted)
            {
                if (!File.Exists(activeFile.DecryptedPath))
                {
                    return activeFile;
                }
                activeFile = new ActiveFile(activeFile, ActiveFileStatus.AssumedOpenAndDecrypted, activeFile.Process);
            }

            return activeFile;
        }

        private ActiveFile CheckIfProcessExited(ActiveFile activeFile)
        {
            if (activeFile.Process == null || IgnoreApplication || !activeFile.Process.HasExited)
            {
                return activeFile;
            }
            if (Logging.IsInfoEnabled)
            {
                Logging.Info("Process exit for '{0}'".InvariantFormat(activeFile.DecryptedPath));
            }
            activeFile = new ActiveFile(activeFile, activeFile.Status & ~ActiveFileStatus.NotShareable, null);
            return activeFile;
        }

        private ActiveFile CheckIfTimeToUpdate(ActiveFile activeFile)
        {
            if (activeFile.Status.HasFlag(ActiveFileStatus.NotShareable) || !activeFile.Status.HasFlag(ActiveFileStatus.AssumedOpenAndDecrypted))
            {
                return activeFile;
            }
            if (activeFile.Key == null)
            {
                return activeFile;
            }
            if (!activeFile.IsModified)
            {
                return activeFile;
            }

            try
            {
                using (FileStream activeFileStream = File.Open(activeFile.DecryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IRuntimeFileInfo sourceFileInfo = AxCryptEnvironment.Current.FileInfo(activeFile.DecryptedPath);
                    AxCryptFile.WriteToFileWithBackup(activeFile.EncryptedPath, (Stream destination) =>
                    {
                        AxCryptFile.Encrypt(sourceFileInfo, destination, activeFile.Key, AxCryptOptions.EncryptWithCompression, _progressManager.Create(Path.GetFileName(activeFile.DecryptedFileInfo.Name)));
                    });
                }
            }
            catch (IOException)
            {
                if (Logging.IsWarningEnabled && !IgnoreApplication)
                {
                    Logging.Warning("Failed exclusive open modified for '{0}'.".InvariantFormat(activeFile.DecryptedPath));
                }
                activeFile = new ActiveFile(activeFile, activeFile.Status | ActiveFileStatus.NotShareable, activeFile.Process);
                return activeFile;
            }
            if (Logging.IsInfoEnabled)
            {
                Logging.Info("Wrote back '{0}' to '{1}'".InvariantFormat(activeFile.DecryptedPath, activeFile.EncryptedPath));
            }
            activeFile = new ActiveFile(activeFile.EncryptedPath, activeFile.DecryptedPath, activeFile.Key, ActiveFileStatus.AssumedOpenAndDecrypted, activeFile.Process);
            return activeFile;
        }

        private static ActiveFile CheckIfTimeToDelete(ActiveFile activeFile)
        {
            if (!AxCryptEnvironment.Current.IsDesktopWindows)
            {
                return activeFile;
            }
            if (!activeFile.Status.HasFlag(ActiveFileStatus.AssumedOpenAndDecrypted) || activeFile.Status.HasFlag(ActiveFileStatus.NotShareable))
            {
                return activeFile;
            }
            if (activeFile.IsModified)
            {
                return activeFile;
            }

            activeFile = TryDelete(activeFile);
            return activeFile;
        }

        public void PurgeActiveFiles()
        {
            ForEach(false, (ActiveFile activeFile) =>
            {
                if (activeFile.Status.HasFlag(ActiveFileStatus.AssumedOpenAndDecrypted) && !activeFile.IsModified)
                {
                    activeFile = TryDelete(activeFile);
                }
                return activeFile;
            });
        }

        private static ActiveFile TryDelete(ActiveFile activeFile)
        {
            FileInfo activeFileInfo = new FileInfo(activeFile.DecryptedPath);

            if (activeFile.IsModified)
            {
                if (Logging.IsInfoEnabled)
                {
                    Logging.Info("Tried delete '{0}' but it is modified.".InvariantFormat(activeFile.DecryptedPath));
                }
                return activeFile;
            }

            try
            {
                if (Logging.IsInfoEnabled)
                {
                    Logging.Info("Deleting '{0}'.".InvariantFormat(activeFile.DecryptedPath));
                }
                activeFileInfo.Delete();
            }
            catch (IOException)
            {
                if (Logging.IsErrorEnabled)
                {
                    Logging.Error("Delete failed for '{0}'".InvariantFormat(activeFileInfo.FullName));
                }
                activeFile = new ActiveFile(activeFile, activeFile.Status | ActiveFileStatus.NotShareable, activeFile.Process);
                return activeFile;
            }

            activeFile = new ActiveFile(activeFile, ActiveFileStatus.NotDecrypted, null);

            if (Logging.IsInfoEnabled)
            {
                Logging.Info("Deleted '{0}' from '{1}'.".InvariantFormat(activeFile.DecryptedPath, activeFile.EncryptedPath));
            }

            return activeFile;
        }

        private DirectoryInfo _temporaryDirectoryInfo;

        public DirectoryInfo TemporaryDirectoryInfo
        {
            get
            {
                if (_temporaryDirectoryInfo == null)
                {
                    string temporaryFolderPath = Path.Combine(Path.GetTempPath(), "AxCrypt");
                    DirectoryInfo temporaryFolderInfo = new DirectoryInfo(temporaryFolderPath);
                    temporaryFolderInfo.Create();
                    _temporaryDirectoryInfo = temporaryFolderInfo;
                }

                return _temporaryDirectoryInfo;
            }
        }

        public void TemporaryDirectoryWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            ActiveFile changedFile = FileSystemState.Current.FindDecryptedPath(e.FullPath);
            if (changedFile == null)
            {
                return;
            }
        }

        public static ActiveFile FindActiveFile(string path)
        {
            return FileSystemState.Current.FindEncryptedPath(path);
        }
    }
}