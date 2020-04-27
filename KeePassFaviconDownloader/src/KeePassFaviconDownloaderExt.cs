/*
  KeePass Favicon Downloader - KeePass plugin that downloads and stores
  favicons for entries with web URLs.
  Copyright (C) 2009-2014 Chris Tomlinson <luckyrat@users.sourceforge.net>
  Thanks to mausoma and psproduction for their contributions

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

  Uses HtmlAgilityPack under MS-PL license: http://htmlagilitypack.codeplex.com/
*/

using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

using KeePass.Plugins;
using KeePass.Forms;

using KeePassLib;
using KeePassLib.Interfaces;

namespace KeePassFaviconDownloader
{
    public sealed class KeePassFaviconDownloaderExt : Plugin
    {
        // The plugin remembers its host in this variable.
        private IPluginHost m_host = null;

        public override string UpdateUrl
        {
            get { return "https://raw.github.com/luckyrat/KeePass-Favicon-Downloader/master/versionInfo.txt"; }
        }

        private ToolStripSeparator m_tsSeparator1 = null;
        private ToolStripSeparator m_tsSeparator2 = null;
        private ToolStripSeparator m_tsSeparator3 = null;
        private ToolStripMenuItem menuDownloadFavicons = null;
        private ToolStripMenuItem menuDownloadGroupFavicons = null;
        private ToolStripMenuItem menuDownloadEntryFavicons = null;

        /// <summary>
        /// Initializes the plugin using the specified KeePass host.
        /// </summary>
        /// <param name="host">The plugin host.</param>
        /// <returns></returns>
        public override bool Initialize(IPluginHost host)
        {
            Debug.Assert(host != null);
            if (host == null) return false;
            m_host = host;

            // Add a seperator and menu item to the 'Tools' menu
            ToolStripItemCollection tsMenu = m_host.MainWindow.ToolsMenu.DropDownItems;
            m_tsSeparator1 = new ToolStripSeparator();
            tsMenu.Add(m_tsSeparator1);
            menuDownloadFavicons = new ToolStripMenuItem();
            menuDownloadFavicons.Text = "Download Favicons for all entries";
            menuDownloadFavicons.Click += OnMenuDownloadFavicons;
            tsMenu.Add(menuDownloadFavicons);

            // Add a seperator and menu item to the group context menu
            ContextMenuStrip gcm = m_host.MainWindow.GroupContextMenu;
            m_tsSeparator2 = new ToolStripSeparator();
            gcm.Items.Add(m_tsSeparator2);
            menuDownloadGroupFavicons = new ToolStripMenuItem();
            menuDownloadGroupFavicons.Text = "Download Favicons";
            menuDownloadGroupFavicons.Click += OnMenuDownloadGroupFavicons;
            gcm.Items.Add(menuDownloadGroupFavicons);

            // Add a seperator and menu item to the entry context menu
            ContextMenuStrip ecm = m_host.MainWindow.EntryContextMenu;
            m_tsSeparator3 = new ToolStripSeparator();
            ecm.Items.Add(m_tsSeparator3);
            menuDownloadEntryFavicons = new ToolStripMenuItem();
            menuDownloadEntryFavicons.Text = "Download Favicons";
            menuDownloadEntryFavicons.Click += OnMenuDownloadEntryFavicons;
            ecm.Items.Add(menuDownloadEntryFavicons);

            return true; // Initialization successful
        }

        /// <summary>
        /// Terminates this instance.
        /// </summary>
        public override void Terminate()
        {
            // Remove 'Tools' menu items
            ToolStripItemCollection tsMenu = m_host.MainWindow.ToolsMenu.DropDownItems;
            tsMenu.Remove(m_tsSeparator1);
            tsMenu.Remove(menuDownloadFavicons);

            // Remove group context menu items
            ContextMenuStrip gcm = m_host.MainWindow.GroupContextMenu;
            gcm.Items.Remove(m_tsSeparator2);
            gcm.Items.Remove(menuDownloadGroupFavicons);

            // Remove entry context menu items
            ContextMenuStrip ecm = m_host.MainWindow.EntryContextMenu;
            ecm.Items.Remove(m_tsSeparator3);
            ecm.Items.Remove(menuDownloadEntryFavicons);
        }

        /// <summary>
        /// Downloads favicons for every entry in the database
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMenuDownloadFavicons(object sender, EventArgs e)
        {
            if (!m_host.Database.IsOpen)
            {
                MessageBox.Show("Please open a database first.", "Favicon downloader");
                return;
            }

            KeePassLib.Collections.PwObjectList<PwEntry> output;
            output = m_host.Database.RootGroup.GetEntries(true);
            DownloadSomeFavicons(output);
        }

        /// <summary>
        /// Downloads favicons for every entry in the selected groups
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMenuDownloadGroupFavicons(object sender, EventArgs e)
        {
            PwGroup pg = m_host.MainWindow.GetSelectedGroup();
            Debug.Assert(pg != null); if (pg == null) return;
            DownloadSomeFavicons(pg.Entries);
        }

        /// <summary>
        /// Downloads favicons for every selected entry
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMenuDownloadEntryFavicons(object sender, EventArgs e)
        {

            PwEntry[] pwes = m_host.MainWindow.GetSelectedEntries();
            Debug.Assert(pwes != null); if (pwes == null || pwes.Length == 0) return;
            DownloadSomeFavicons(KeePassLib.Collections.PwObjectList<PwEntry>.FromArray(pwes));
        }

        /// <summary>
        /// Downloads some favicons.
        /// </summary>
        /// <param name="entries">The entries.</param>
        private void DownloadSomeFavicons(KeePassLib.Collections.PwObjectList<PwEntry> entries)
        {
            StatusProgressForm progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Favicons", true, false, m_host.MainWindow);
            progressForm.Show();
            progressForm.SetProgress(0);

            float progress = 0;
            float outputLength = (float)entries.UCount;
            int downloadsCompleted = 0;
            string errorMessage = "";
            int errorCount = 0;

            // TODO use await foreach .WithCancellation() (needs .NET 4.8 though)?
            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8
            foreach (PwEntry pwe in entries)
            {
                progressForm.SetText("Title: " + pwe.Strings.ReadSafe("Title") + "; User Name: " + pwe.Strings.ReadSafe("UserName"), LogStatusType.Info);

                try
                {
                    DownloadOneFavicon(pwe);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    errorCount++;
                }

                downloadsCompleted++;
                progress = (downloadsCompleted / outputLength) * 100;
                progressForm.SetProgress((uint)Math.Floor(progress));
                System.Threading.Thread.Sleep(100);
                if (progressForm.UserCancelled)
                    break;
            }

            progressForm.Hide();
            progressForm.Close();

            if (errorMessage != "")
            {
                if (errorCount == 1)
                    MessageBox.Show(errorMessage, "Download error");
                else
                    MessageBox.Show(errorCount + " errors occurred. The last error message is shown here. To see the other messages, select a smaller group of entries and use the right click menu to start the download.\n" + errorMessage, "Download errors");
            }

            // Mark as modified only if there was any change
            if (m_host.Database.UINeedsIconUpdate)
            {
                m_host.MainWindow.UpdateUI(false, null, false, null, true, null, true);
                m_host.MainWindow.UpdateTrayIcon();
            }
        }

        /// <summary>
        /// Downloads one favicon and attaches it to the entry
        /// </summary>
        /// <param name="pwe">The entry for which we want to download the favicon</param>
        private void DownloadOneFavicon(PwEntry pwe)
        {
            // Read URL
            string url = pwe.Strings.ReadSafe("URL");

            // Use title of no URL is given
            if (string.IsNullOrEmpty(url))
                url = pwe.Strings.ReadSafe("Title");

            // If we still have no URL, quit
            if (string.IsNullOrEmpty(url))
                return;

            // Parse website and load favicon
            MemoryStream ms = FaviconDownloader.GetFuzzyForWebsite(url);
            Debug.Assert(ms != null);

            byte[] msByteArray = ms.ToArray();

            foreach (PwCustomIcon item in m_host.Database.CustomIcons)
            {
                // re-use existing custom icon if it's already in the database
                // (This will probably fail if database is used on
                // both 32 bit and 64 bit machines - not sure why...)
                if (KeePassLib.Utility.MemUtil.ArraysEqual(msByteArray, item.ImageDataPng))
                {
                    if (!pwe.CustomIconUuid.Equals(item.Uuid))
                    {
                        pwe.CustomIconUuid = item.Uuid;
                        pwe.Touch(true);
                        m_host.Database.UINeedsIconUpdate = true;
                    }
                    return;
                }
            }

            // Create a new custom icon for use with this entry
            PwCustomIcon pwci = new PwCustomIcon(new PwUuid(true), ms.ToArray());
            m_host.Database.CustomIcons.Add(pwci);
            pwe.CustomIconUuid = pwci.Uuid;
            pwe.Touch(true);
            m_host.Database.UINeedsIconUpdate = true;
        }
    }
}
