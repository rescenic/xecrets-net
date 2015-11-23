﻿using Axantum.AxCrypt.Core;
using Axantum.AxCrypt.Core.Extensions;
using Axantum.AxCrypt.Core.IO;
using Axantum.AxCrypt.Core.Runtime;
using Axantum.AxCrypt.Core.Session;
using Axantum.AxCrypt.Forms.Style;
using Axantum.AxCrypt.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Axantum.AxCrypt
{
    public class RecentFilesListView : ListView
    {
        private enum ImageKey
        {
            ActiveFile,
            Exclamation,
            DecryptedFile,
            DecryptedUnknownKeyFile,
            ActiveFileKnownKey,
            CleanUpNeeded,
            KeyShared,
            LowEncryption,
        }

        private enum ColumnName
        {
            DocumentName,
            SharingIndicator,
            Date,
            EncryptedPath,
            CryptoName,
        }

        public RecentFilesListView()
        {
            DoubleBuffered = true;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            if (DesignMode)
            {
                return;
            }

            SmallImageList = CreateSmallImageListToAvoidLocalizationIssuesWithDesignerAndResources();
            LargeImageList = CreateLargeImageListToAvoidLocalizationIssuesWithDesignerAndResources();

            ColumnWidthChanged += RecentFilesListView_ColumnWidthChanged;
            RestoreUserPreferences();
        }

        private void RecentFilesListView_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            switch (e.ColumnIndex)
            {
                case 0:
                    Preferences.RecentFilesDocumentWidth = Columns[e.ColumnIndex].Width;
                    break;

                case 2:
                    Preferences.RecentFilesDateTimeWidth = Columns[e.ColumnIndex].Width;
                    break;

                case 3:
                    Preferences.RecentFilesEncryptedPathWidth = Columns[e.ColumnIndex].Width;
                    break;

                case 4:
                    Preferences.RecentFilesCryptoNameWidth = Columns[e.ColumnIndex].Width;
                    break;
            }
        }

        private void RestoreUserPreferences()
        {
            Preferences.RecentFilesMaxNumber = 100;

            Columns[0].Width = Preferences.RecentFilesDocumentWidth.Fallback(Columns[0].Width);
            Columns[2].Width = Preferences.RecentFilesDateTimeWidth.Fallback(Columns[2].Width);
            Columns[3].Width = Preferences.RecentFilesEncryptedPathWidth.Fallback(Columns[3].Width);
            Columns[4].Width = Preferences.RecentFilesCryptoNameWidth.Fallback(Columns[4].Width);
        }

        private bool _updateRecentFilesInProgress = false;

        public async Task UpdateRecentFilesAsync(IEnumerable<ActiveFile> files, LicensePolicy license)
        {
            if (_updateRecentFilesInProgress)
            {
                return;
            }
            _updateRecentFilesInProgress = true;
            try
            {
                Cursor = Cursors.WaitCursor;
                await UpdateRecentFilesUnsynchronizedAsync(files, license);
            }
            finally
            {
                _updateRecentFilesInProgress = false;
                Cursor = Cursors.Default;
            }
        }

        private async Task UpdateRecentFilesUnsynchronizedAsync(IEnumerable<ActiveFile> files, LicensePolicy license)
        {
            BeginUpdate();
            try
            {
                Dictionary<string, int> currentFiles = RemoveRemovedFilesFromRecent(files);

                List<ListViewItem> newItems = new List<ListViewItem>();
                foreach (ActiveFile file in files)
                {
                    await UpdateOneItemAsync(currentFiles, newItems, file, license);
                }

                while (Items.Count > Preferences.RecentFilesMaxNumber)
                {
                    Items.RemoveAt(Items.Count - 1);
                }
            }
            finally
            {
                EndUpdate();
            }
        }

        private async Task UpdateOneItemAsync(Dictionary<string, int> currentFiles, List<ListViewItem> newItems, ActiveFile file, LicensePolicy license)
        {
            string text = Path.GetFileName(file.DecryptedFileInfo.FullName);
            ListViewItem item = new ListViewItem(text);
            item.UseItemStyleForSubItems = false;
            item.Name = file.EncryptedFileInfo.FullName;

            ListViewItem.ListViewSubItem sharingIndicatorColumn = item.SubItems.Add(String.Empty);
            sharingIndicatorColumn.Name = nameof(ColumnName.SharingIndicator);

            ListViewItem.ListViewSubItem dateColumn = item.SubItems.Add(String.Empty);
            dateColumn.Name = nameof(ColumnName.Date);

            ListViewItem.ListViewSubItem encryptedPathColumn = item.SubItems.Add(String.Empty);
            encryptedPathColumn.Name = nameof(ColumnName.EncryptedPath);

            ListViewItem.ListViewSubItem cryptoNameColumn = item.SubItems.Add(String.Empty);
            cryptoNameColumn.Name = nameof(ColumnName.CryptoName);

            await UpdateListViewItemAsync(item, file, license);
            int i;
            if (!currentFiles.TryGetValue(item.Name, out i))
            {
                Items.Add(item);
                return;
            }

            if (!CompareRecentFileItem(item, Items[i]))
            {
                Items[i] = item;
            }
        }

        private Dictionary<string, int> RemoveRemovedFilesFromRecent(IEnumerable<ActiveFile> files)
        {
            HashSet<string> newFiles = new HashSet<string>(files.Select(f => f.EncryptedFileInfo.FullName));
            Dictionary<string, int> currentFiles = new Dictionary<string, int>();
            for (int i = 0; i < Items.Count;)
            {
                if (!newFiles.Contains(Items[i].Name))
                {
                    Items.RemoveAt(i);
                    continue;
                }
                currentFiles.Add(Items[i].Name, i);
                ++i;
            }

            return currentFiles;
        }

        private static bool CompareRecentFileItem(ListViewItem left, ListViewItem right)
        {
            if (left.SubItems[nameof(ColumnName.EncryptedPath)].Text != right.SubItems[nameof(ColumnName.EncryptedPath)].Text)
            {
                return false;
            }
            if (left.SubItems[nameof(ColumnName.SharingIndicator)].Text != right.SubItems[nameof(ColumnName.SharingIndicator)].Text)
            {
                return false;
            }
            if (left.SubItems[nameof(ColumnName.CryptoName)].Text != right.SubItems[nameof(ColumnName.CryptoName)].Text)
            {
                return false;
            }
            if (left.SubItems[nameof(ColumnName.CryptoName)].ForeColor != right.SubItems[nameof(ColumnName.CryptoName)].ForeColor)
            {
                return false;
            }
            if (left.ImageKey != right.ImageKey)
            {
                return false;
            }
            if (left.SubItems[nameof(ColumnName.Date)].Text != right.SubItems[nameof(ColumnName.Date)].Text)
            {
                return false;
            }
            if ((DateTime)left.SubItems[nameof(ColumnName.Date)].Tag != (DateTime)right.SubItems[nameof(ColumnName.Date)].Tag)
            {
                return false;
            }
            return true;
        }

        private async Task UpdateListViewItemAsync(ListViewItem item, ActiveFile activeFile, LicensePolicy license)
        {
            EncryptedProperties encryptedProperties = await EncryptedPropertiesAsync(activeFile.EncryptedFileInfo);
            string sharingIndicator = SharingIndicator(encryptedProperties.SharedKeyHolders.Count());
            item.SubItems[nameof(ColumnName.EncryptedPath)].Text = activeFile.EncryptedFileInfo.FullName;
            item.SubItems[nameof(ColumnName.SharingIndicator)].Text = sharingIndicator;
            item.SubItems[nameof(ColumnName.Date)].Text = activeFile.Properties.LastActivityTimeUtc.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            item.SubItems[nameof(ColumnName.Date)].Tag = activeFile.Properties.LastActivityTimeUtc;

            UpdateStatusDependentPropertiesOfListViewItem(item, activeFile, sharingIndicator);

            try
            {
                if (activeFile.Properties.CryptoId != Guid.Empty)
                {
                    item.SubItems[nameof(ColumnName.CryptoName)].Text = Resolve.CryptoFactory.Create(activeFile.Properties.CryptoId).Name;
                    if (activeFile.VisualState.HasFlag(ActiveFileVisualState.LowEncryption) && !license.Has(LicenseCapability.StrongerEncryption))
                    {
                        item.SubItems[nameof(ColumnName.CryptoName)].ForeColor = Styling.WarningColor;
                    }
                }
            }
            catch (ArgumentException)
            {
                item.SubItems[nameof(ColumnName.CryptoName)].Text = Resources.UnknownCrypto;
            }
        }

        private void UpdateStatusDependentPropertiesOfListViewItem(ListViewItem item, ActiveFile activeFile, string sharingIndicator)
        {
            switch (activeFile.VisualState & ~(ActiveFileVisualState.SharedKeys | ActiveFileVisualState.LowEncryption))
            {
                case ActiveFileVisualState.DecryptedWithKnownKey:
                case ActiveFileVisualState.DecryptedWithoutKnownKey:
                    item.ImageKey = nameof(ImageKey.CleanUpNeeded);
                    item.ToolTipText = Resources.CleanUpNeededToolTip;
                    return;
            }

            if (!String.IsNullOrEmpty(sharingIndicator))
            {
                item.ImageKey = nameof(ImageKey.KeyShared);
                item.ToolTipText = Resources.KeySharingExistsToolTip;
                return;
            }

            item.ImageKey = String.Empty;
            item.ToolTipText = Resources.DoubleClickToOpenToolTip;
            return;
        }

        private static string SharingIndicator(int count)
        {
            if (count < 2)
            {
                return String.Empty;
            }
            return count.ToString(CultureInfo.CurrentCulture);
        }

        private static async Task<EncryptedProperties> EncryptedPropertiesAsync(IDataStore dataStore)
        {
            return await Task.Run(() => EncryptedProperties.Create(dataStore));
        }

        private static ImageList CreateSmallImageListToAvoidLocalizationIssuesWithDesignerAndResources()
        {
            ImageList smallImageList = new ImageList();

            smallImageList.Images.Add(nameof(ImageKey.ActiveFile), Resources.activefilegreen16);
            smallImageList.Images.Add(nameof(ImageKey.Exclamation), Resources.exclamationgreen16);
            smallImageList.Images.Add(nameof(ImageKey.DecryptedFile), Resources.decryptedfilered16);
            smallImageList.Images.Add(nameof(ImageKey.DecryptedUnknownKeyFile), Resources.decryptedunknownkeyfilered16);
            smallImageList.Images.Add(nameof(ImageKey.ActiveFileKnownKey), Resources.fileknownkeygreen16);
            smallImageList.Images.Add(nameof(ImageKey.CleanUpNeeded), Resources.clean_broom_red);
            smallImageList.Images.Add(nameof(ImageKey.KeyShared), Resources.share_80px);
            smallImageList.TransparentColor = System.Drawing.Color.Transparent;

            return smallImageList;
        }

        private static ImageList CreateLargeImageListToAvoidLocalizationIssuesWithDesignerAndResources()
        {
            ImageList largeImageList = new ImageList();

            largeImageList.Images.Add(nameof(ImageKey.ActiveFile), Resources.opendocument32);
            largeImageList.Images.Add(nameof(ImageKey.Exclamation), Resources.exclamationgreen32);
            largeImageList.TransparentColor = System.Drawing.Color.Transparent;

            return largeImageList;
        }
    }
}