﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Axantum.AxCrypt.Core;
using Axantum.AxCrypt.Core.Crypto;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.UI;
using Axantum.AxCrypt.Properties;

namespace Axantum.AxCrypt
{
    public partial class AxCryptMainForm : Form
    {
        public static MessageBoxOptions MessageBoxOptions { get; private set; }

        private ProgressManager _progressManager;

        private EncryptedFileManager _encryptedFileManager;

        private IDictionary<BackgroundWorker, ProgressBar> _progressBars = new Dictionary<BackgroundWorker, ProgressBar>();

        private void FormatTraceMessage(string message)
        {
            int skipIndex = message.IndexOf(" Information", StringComparison.Ordinal);
            skipIndex = skipIndex < 0 ? message.IndexOf(" Warning", StringComparison.Ordinal) : skipIndex;
            skipIndex = skipIndex < 0 ? message.IndexOf(" Debug", StringComparison.Ordinal) : skipIndex;
            skipIndex = skipIndex < 0 ? message.IndexOf(" Error", StringComparison.Ordinal) : skipIndex;
            InvokeIfRequired(() =>
            {
                LogOutput.AppendText(message.Substring(skipIndex + 1));
            });
        }

        public AxCryptMainForm()
        {
            InitializeComponent();
        }

        private void AxCryptMainForm_Load(object sender, EventArgs e)
        {
            DelegateTraceListener traceListener = new DelegateTraceListener(FormatTraceMessage);
            traceListener.Name = "AxCryptMainFormListener";
            Trace.Listeners.Add(traceListener);

            _progressManager = new ProgressManager();
            _progressManager.Progress += new EventHandler<ProgressEventArgs>(ProgressManager_Progress);
            _encryptedFileManager = new EncryptedFileManager(_progressManager);
            MessageBoxOptions = RightToLeft == RightToLeft.Yes ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading : 0;

            while (_fileOperationInProgress)
            {
                Application.DoEvents();
            }
            try
            {
                _fileOperationInProgress = true;
                _encryptedFileManager.IgnoreApplication = !AxCryptEnvironment.Current.IsDesktopWindows;
                _encryptedFileManager.Changed += new EventHandler<EventArgs>(EncryptedFileManager_Changed);
                _encryptedFileManager.ForceActiveFilesStatus();
            }
            finally
            {
                _fileOperationInProgress = false;
            }
            UserPreferences userPreferences = Settings.Default.UserPreferences;
            if (userPreferences == null)
            {
                userPreferences = new UserPreferences();
                Settings.Default.UserPreferences = userPreferences;
                Settings.Default.Save();
                return;
            }
            RecentFilesListView.Columns[0].Name = "DecryptedFile";
            RecentFilesListView.Columns[0].Width = userPreferences.RecentFilesDocumentWidth > 0 ? userPreferences.RecentFilesDocumentWidth : RecentFilesListView.Columns[0].Width;
        }

        private void ProgressManager_Progress(object sender, ProgressEventArgs e)
        {
            Application.DoEvents();
            BackgroundWorker worker = e.Context as BackgroundWorker;
            if (worker == null)
            {
                return;
            }
            worker.ReportProgress(e.Percent, worker);
        }

        private void EncryptedFileManager_Changed(object sender, EventArgs e)
        {
            InvokeIfRequired(RestartTimer);
        }

        private void InvokeIfRequired(Action action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private void RestartTimer()
        {
            ActiveFilePolling.Stop();
            ActiveFilePolling.Enabled = false;
            ActiveFilePolling.Interval = 1000;
            ActiveFilePolling.Enabled = true;
            ActiveFilePolling.Start();
        }

        private void UpdateActiveFileState()
        {
            OpenFilesListView.Items.Clear();
            RecentFilesListView.Items.Clear();
            _encryptedFileManager.ForEach(false, (ActiveFile activeFile) => { return UpdateOpenFilesWith(activeFile); });
        }

        private ActiveFile UpdateOpenFilesWith(ActiveFile activeFile)
        {
            ListViewItem item;
            if (activeFile.Status.HasFlag(ActiveFileStatus.NotDecrypted))
            {
                if (String.IsNullOrEmpty(activeFile.DecryptedPath))
                {
                    item = new ListViewItem(String.Empty, "InactiveFile");
                }
                else
                {
                    item = new ListViewItem(Path.GetFileName(activeFile.DecryptedPath), "ActiveFile");
                }

                ListViewItem.ListViewSubItem dateColumn = new ListViewItem.ListViewSubItem();
                dateColumn.Text = activeFile.LastAccessTimeUtc.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                dateColumn.Tag = activeFile.LastAccessTimeUtc;
                dateColumn.Name = "Date";
                item.SubItems.Add(dateColumn);

                ListViewItem.ListViewSubItem encryptedPathColumn = new ListViewItem.ListViewSubItem();
                encryptedPathColumn.Name = "EncryptedPath";
                encryptedPathColumn.Text = activeFile.EncryptedPath;
                item.SubItems.Add(encryptedPathColumn);

                RecentFilesListView.Items.Add(item);
            }
            if (activeFile.Status.HasFlag(ActiveFileStatus.DecryptedIsPendingDelete) || activeFile.Status.HasFlag(ActiveFileStatus.AssumedOpenAndDecrypted))
            {
                item = new ListViewItem(Path.GetFileName(activeFile.DecryptedPath), activeFile.Key != null ? "ActiveFile" : "Exclamation");
                ListViewItem.ListViewSubItem encryptedPathColumn = new ListViewItem.ListViewSubItem();
                encryptedPathColumn.Name = "EncryptedPath";
                encryptedPathColumn.Text = activeFile.EncryptedPath;
                item.SubItems.Add(encryptedPathColumn);

                OpenFilesListView.Items.Add(item);
            }
            return activeFile;
        }

        private void toolStripButtonEncrypt_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = Resources.EncryptFileOpenDialogTitle;
                ofd.Multiselect = true;
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                DialogResult result = ofd.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return;
                }
                foreach (string file in ofd.FileNames)
                {
                    EncryptFile(file);
                }
            }
        }

        private void EncryptFile(string file)
        {
            if (String.Compare(Path.GetExtension(file), AxCryptEnvironment.Current.AxCryptExtension, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return;
            }

            IRuntimeFileInfo sourceFileInfo = AxCryptEnvironment.Current.FileInfo(file);
            IRuntimeFileInfo destinationFileInfo = AxCryptEnvironment.Current.FileInfo(AxCryptFile.MakeAxCryptFileName(sourceFileInfo));
            if (destinationFileInfo.Exists)
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = Resources.EncryptFileSaveAsDialogTitle;
                    sfd.AddExtension = true;
                    sfd.ValidateNames = true;
                    sfd.CheckPathExists = true;
                    sfd.DefaultExt = AxCryptEnvironment.Current.AxCryptExtension;
                    sfd.FileName = destinationFileInfo.FullName;
                    sfd.Filter = Resources.EncryptedFileDialogFilterPattern.InvariantFormat(AxCryptEnvironment.Current.AxCryptExtension);
                    sfd.InitialDirectory = Path.GetDirectoryName(destinationFileInfo.FullName);
                    sfd.ValidateNames = true;
                    DialogResult saveAsResult = sfd.ShowDialog();
                    if (saveAsResult != DialogResult.OK)
                    {
                        return;
                    }
                    destinationFileInfo = AxCryptEnvironment.Current.FileInfo(sfd.FileName);
                }
            }

            AesKey key = null;
            if (KnownKeys.DefaultEncryptionKey == null)
            {
                EncryptPassphraseDialog passphraseDialog = new EncryptPassphraseDialog();
                DialogResult dialogResult = passphraseDialog.ShowDialog();
                if (dialogResult != DialogResult.OK)
                {
                    return;
                }
                Passphrase passphrase = new Passphrase(passphraseDialog.PassphraseTextBox.Text);
                key = passphrase.DerivedPassphrase;
            }
            else
            {
                key = KnownKeys.DefaultEncryptionKey;
            }

            BackgroundWorker worker = CreateWorker();
            worker.DoWork += (object sender, DoWorkEventArgs e) =>
            {
                WorkerArguments arguments = (WorkerArguments)e.Argument;
                EncryptedFileManager.EncryptFile(arguments.SourceFileInfo, arguments.DestinationFileInfo, arguments.Key, arguments.Progress);
            };
            worker.RunWorkerAsync(new WorkerArguments(sourceFileInfo, destinationFileInfo, key, _progressManager.Create(sourceFileInfo.FullName, worker)));

            if (KnownKeys.DefaultEncryptionKey == null)
            {
                KnownKeys.DefaultEncryptionKey = key;
            }
        }

        private BackgroundWorker CreateWorker()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);

            ProgressBar progressBar = new ProgressBar();
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            ProgressPanel.Controls.Add(progressBar);
            progressBar.Dock = DockStyle.Fill;
            progressBar.Margin = new Padding(0);
            _progressBars.Add(worker, progressBar);
            return worker;
        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)e.UserState;
            ProgressBar progressBar = _progressBars[worker];
            progressBar.Value = e.ProgressPercentage;
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            ProgressBar progressBar = _progressBars[worker];
            progressBar.Parent = null;
            _progressBars.Remove(worker);
            worker.Dispose();
        }

        private void toolStripButtonDecrypt_Click(object sender, EventArgs e)
        {
            string[] fileNames;
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = Resources.DecryptFileOpenDialogTitle;
                ofd.Multiselect = true;
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                ofd.DefaultExt = AxCryptEnvironment.Current.AxCryptExtension;
                ofd.Filter = Resources.EncryptedFileDialogFilterPattern.InvariantFormat("{0}".InvariantFormat(AxCryptEnvironment.Current.AxCryptExtension));
                ofd.Multiselect = true;
                ofd.Title = Resources.DecryptFileOpenDialogTitle;
                DialogResult result = ofd.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return;
                }
                fileNames = ofd.FileNames;
            }
            foreach (string file in fileNames)
            {
                if (!DecryptFile(file))
                {
                    return;
                }
            }
        }

        private bool DecryptFile(string file)
        {
            IRuntimeFileInfo source = AxCryptEnvironment.Current.FileInfo(file);
            AxCryptDocument document = null;
            try
            {
                if (!DecryptFileInternal(source, ref document))
                {
                    return false;
                }
            }
            finally
            {
                if (document != null)
                {
                    document.Dispose();
                }
            }
            AxCryptFile.Wipe(source);
            return true;
        }

        private bool DecryptFileInternal(IRuntimeFileInfo source, ref AxCryptDocument document)
        {
            foreach (AesKey key in KnownKeys.Keys)
            {
                document = AxCryptFile.Document(source, key, _progressManager.Create(Path.GetFileName(source.FullName)));
                if (document.PassphraseIsValid)
                {
                    break;
                }
            }
            if (document == null || !document.PassphraseIsValid)
            {
                Passphrase passphrase;
                do
                {
                    DecryptPassphraseDialog passphraseDialog = new DecryptPassphraseDialog();
                    DialogResult dialogResult = passphraseDialog.ShowDialog();
                    if (dialogResult != DialogResult.OK)
                    {
                        return false;
                    }
                    passphrase = new Passphrase(passphraseDialog.Passphrase.Text);
                    document = AxCryptFile.Document(source, passphrase.DerivedPassphrase, _progressManager.Create(Path.GetFileName(source.FullName)));
                } while (!document.PassphraseIsValid);
            }

            IRuntimeFileInfo destination = AxCryptEnvironment.Current.FileInfo(Path.Combine(Path.GetDirectoryName(source.FullName), document.DocumentHeaders.FileName));
            if (destination.Exists)
            {
                string extension = Path.GetExtension(destination.FullName);
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.AddExtension = !String.IsNullOrEmpty(extension);
                sfd.CheckPathExists = true;
                sfd.DefaultExt = extension;
                sfd.Filter = Resources.DecryptedSaveAsFileDialogFilterPattern.InvariantFormat(extension);
                sfd.InitialDirectory = Path.GetDirectoryName(source.FullName);
                sfd.OverwritePrompt = true;
                sfd.RestoreDirectory = true;
                sfd.Title = Resources.DecryptedSaveAsFileDialogTitle;
                DialogResult result = sfd.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return false;
                }
                destination = AxCryptEnvironment.Current.FileInfo(sfd.FileName);
            }

            AxCryptFile.Decrypt(document, destination, AxCryptOptions.SetFileTimes);

            AddKnownKey(document.DocumentHeaders.KeyEncryptingKey);

            return true;
        }

        private void AddKnownKey(AesKey key)
        {
            KnownKeys.Add(key);
            _encryptedFileManager.CheckActiveFilesStatus();
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void toolStripButtonOpenEncrypted_Click(object sender, EventArgs e)
        {
            OpenDialog();
        }

        private void OpenDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = Resources.OpenEncryptedFileOpenDialogTitle;
                ofd.Multiselect = false;
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                ofd.DefaultExt = AxCryptEnvironment.Current.AxCryptExtension;
                ofd.Filter = Resources.EncryptedFileDialogFilterPattern.InvariantFormat("{0}".InvariantFormat(AxCryptEnvironment.Current.AxCryptExtension));
                DialogResult result = ofd.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return;
                }

                foreach (string file in ofd.FileNames)
                {
                    OpenEncrypted(file);
                }
            }
        }

        private void OpenEncrypted(string file)
        {
            while (_fileOperationInProgress)
            {
                Application.DoEvents();
            }
            try
            {
                _fileOperationInProgress = true;
                if (_encryptedFileManager.Open(file, KnownKeys.Keys, _progressManager.Create(Path.GetFileName(file))) != FileOperationStatus.InvalidKey)
                {
                    return;
                }

                Passphrase passphrase;
                FileOperationStatus status;
                do
                {
                    passphrase = AskForDecryptPassphrase();
                    if (passphrase == null)
                    {
                        return;
                    }
                    status = _encryptedFileManager.Open(file, new AesKey[] { passphrase.DerivedPassphrase }, _progressManager.Create(Path.GetFileName(file)));
                } while (status == FileOperationStatus.InvalidKey);
                if (status != FileOperationStatus.Success)
                {
                    "Failed to decrypt and open {0}".InvariantFormat(file).ShowWarning();
                }
                else
                {
                    AddKnownKey(passphrase.DerivedPassphrase);
                }
            }
            finally
            {
                _fileOperationInProgress = false;
            }
        }

        private Passphrase AskForDecryptPassphrase()
        {
            DecryptPassphraseDialog passphraseDialog = new DecryptPassphraseDialog();
            DialogResult dialogResult = passphraseDialog.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                return null;
            }
            Passphrase passphrase = new Passphrase(passphraseDialog.Passphrase.Text);
            return passphrase;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private bool _fileOperationInProgress = false;

        private void ActiveFilePolling_Tick(object sender, EventArgs e)
        {
            if (_fileOperationInProgress)
            {
                return;
            }
            ActiveFilePolling.Stop();
            ActiveFilePolling.Enabled = false;
            try
            {
                _fileOperationInProgress = true;
                _encryptedFileManager.CheckActiveFilesStatus();
            }
            finally
            {
                _fileOperationInProgress = false;
            }
            UpdateActiveFileState();
        }

        private void openEncryptedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDialog();
        }

        private void RecentFilesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void RecentFilesListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            string encryptedPath = RecentFilesListView.SelectedItems[0].SubItems["EncryptedPath"].Text;
            OpenEncrypted(encryptedPath);
        }

        private void RecentFilesListView_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            ListView listView = (ListView)sender;
            string columnName = listView.Columns[e.ColumnIndex].Name;
            switch (columnName)
            {
                case "DecryptedFile":
                    Settings.Default.UserPreferences.RecentFilesDocumentWidth = listView.Columns[e.ColumnIndex].Width;
                    break;
            }
            Settings.Default.Save();
        }

        private void AxCryptMainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            PurgeActiveFiles();
            Trace.Listeners.Remove("AxCryptMainFormListener");
        }

        private void PurgeActiveFiles()
        {
            while (_fileOperationInProgress)
            {
                Application.DoEvents();
            }
            try
            {
                _fileOperationInProgress = true;
                _encryptedFileManager.IgnoreApplication = true;
                _encryptedFileManager.CheckActiveFilesStatus();
                _encryptedFileManager.PurgeActiveFiles();
                IList<ActiveFile> openFiles = _encryptedFileManager.FindOpenFiles();
                if (openFiles.Count == 0)
                {
                    return;
                }
                StringBuilder sb = new StringBuilder();
                foreach (ActiveFile openFile in openFiles)
                {
                    sb.Append("{0}\n".InvariantFormat(Path.GetFileName(openFile.DecryptedPath)));
                }
                sb.ToString().ShowWarning();
            }
            finally
            {
                _fileOperationInProgress = false;
            }
        }

        private void OpenFilesListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            string encryptedPath = OpenFilesListView.SelectedItems[0].SubItems["EncryptedPath"].Text;
            OpenEncrypted(encryptedPath);
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string encryptedPath = RecentFilesListView.SelectedItems[0].SubItems["EncryptedPath"].Text;
            _encryptedFileManager.RemoveRecentFile(encryptedPath);
        }

        private void RecentFilesListView_MouseClick(object sender, MouseEventArgs e)
        {
            ShowContextMenu(RecentFilesContextMenu, sender, e);
        }

        private void ShowContextMenu(ContextMenuStrip contextMenu, object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            ListView listView = (ListView)sender;
            contextMenu.Show(listView, e.Location);
        }

        private void CloseStripMenuItem_Click(object sender, EventArgs e)
        {
            PurgeActiveFiles();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            PurgeActiveFiles();
        }

        private void OpenFilesListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            ListView recentFiles = (ListView)sender;
            OpenFilesContextMenu.Show(recentFiles, e.Location);
        }

        private void EnterPassphraseMenuItem_Click(object sender, EventArgs e)
        {
            Passphrase passphrase = AskForDecryptPassphrase();
            if (passphrase == null)
            {
                return;
            }
            bool keyMatch = _encryptedFileManager.UpdateActiveFileIfKeyMatchesThumbprint(passphrase.DerivedPassphrase);
            if (keyMatch)
            {
                KnownKeys.Add(passphrase.DerivedPassphrase);
            }
        }
    }
}