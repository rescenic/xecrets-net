﻿using Axantum.AxCrypt.Abstractions;
using Axantum.AxCrypt.Core.UI.ViewModel;
using Axantum.AxCrypt.Forms.Style;
using Axantum.AxCrypt.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using static Axantum.AxCrypt.Abstractions.TypeResolve;

namespace Axantum.AxCrypt
{
    public partial class WatchedFoldersDialog : Form
    {
        private WatchedFoldersViewModel _viewModel;

        private IEnumerable<string> _additional;

        public WatchedFoldersDialog(Form parent, IEnumerable<string> additional)
        {
            InitializeComponent();
            new Styling(Resources.axcrypticon).Style(this);

            _additional = additional;
            _viewModel = New<WatchedFoldersViewModel>();

            Owner = parent;
            StartPosition = FormStartPosition.CenterParent;
        }

        private void WatchedFoldersDialog_Load(object sender, EventArgs e)
        {
            BindToViewModel();
            _viewModel.AddWatchedFolders.Execute(_additional);
            _additional = new string[0];
        }

        private void BindToViewModel()
        {
            _viewModel.BindPropertyChanged("WatchedFolders", (IEnumerable<string> folders) => { UpdateWatchedFolders(folders); });

            _watchedFoldersListView.SelectedIndexChanged += (sender, e) => { _viewModel.SelectedWatchedFolders = _watchedFoldersListView.SelectedItems.Cast<ListViewItem>().Select(lvi => lvi.Text); };
            _watchedFoldersListView.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Right) _watchedFoldersContextMenuStrip.Show((Control)sender, e.Location); };
            _watchedFoldersListView.DragOver += (sender, e) => { _viewModel.DragAndDropFiles = e.GetDragged(); e.Effect = GetEffectsForWatchedFolders(e); };
            _watchedFoldersListView.DragDrop += (sender, e) => { _viewModel.AddWatchedFolders.Execute(_viewModel.DragAndDropFiles); };
            _watchedFoldersOpenExplorerHereMenuItem.Click += (sender, e) => { _viewModel.OpenSelectedFolder.Execute(_viewModel.SelectedWatchedFolders.First()); };
            _watchedFoldersRemoveMenuItem.Click += (sender, e) => { _viewModel.RemoveWatchedFolders.Execute(_viewModel.SelectedWatchedFolders); };
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

        public DragDropEffects GetEffectsForWatchedFolders(DragEventArgs e)
        {
            if (e == null)
            {
                throw new ArgumentNullException("e");
            }

            if (!_viewModel.DroppableAsWatchedFolder)
            {
                return DragDropEffects.None;
            }
            return (DragDropEffects.Link | DragDropEffects.Copy) & e.AllowedEffect;
        }
    }
}