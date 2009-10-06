/*
  KeePass Favicon Downloader - KeePass plugin that downloads and stores
  favicons for entries with web URLs.
  Copyright (C) 2009 Chris Tomlinson <luckyrat@users.sourceforge.net>

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
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net;
using System.Drawing;

using KeePass.Plugins;
using KeePass.Forms;
using KeePass.Resources;

using KeePassLib;
using KeePassLib.Security;

namespace KeePassFaviconDownloader
{
	public sealed class KeePassFaviconDownloaderExt : Plugin
	{
		// The plugin remembers its host in this variable.
		private IPluginHost m_host = null;

        private ToolStripSeparator m_tsSeparator1 = null;
        private ToolStripSeparator m_tsSeparator2 = null;
        private ToolStripSeparator m_tsSeparator3 = null;
        private ToolStripMenuItem menuDownloadFavicons = null;
        private ToolStripMenuItem menuDownloadGroupFavicons = null;
        private ToolStripMenuItem menuDownloadEntryFavicons = null;

        string defaultImage = @"iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAMAAAFfKj/FAAAABGdBTUEAAK/INwWK6QAAABl0RVh0U29mdHdhcmUAQWRvYmUgSW1hZ2VSZWFkeXHJZTwAAABpUExURf///wAAAAAAAFpaWl5eXm5ubnh4eICAgIeHh5GRkaCgoKOjo66urq+vr8jIyMnJycvLy9LS0uDg4Ovr6+zs7O3t7e7u7u/v7/X19fb29vf39/j4+Pn5+fr6+vv7+/z8/P39/f7+/v///5goWdMAAAADdFJOUwAxTTRG/kEAAACRSURBVBjTTY2JEoMgDESDaO0h9m5DUZT9/49sCDLtzpB5eQwLkSTkwb0cOBnJksYxiHqORHZG3gFc88WReTzvBFoOMbUCVkN/ATw3CnwHmwLjpYCfYoF5TQphAUztMfp5zsm5phY6MEsV+LapYRPAoC/ooOLxfL33RXQifJjjsnZFWPBniksCbBU+6F4FmV+IvtrgDOmaq+PeAAAAAElFTkSuQmCC";

		public override bool Initialize(IPluginHost host)
		{
			Debug.Assert(host != null);
			if(host == null) return false;
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
            var gcm = m_host.MainWindow.GroupContextMenu;
            m_tsSeparator2 = new ToolStripSeparator();
            gcm.Items.Add(m_tsSeparator2);
            menuDownloadGroupFavicons = new ToolStripMenuItem();
            menuDownloadGroupFavicons.Text = "Download Favicons";
            menuDownloadGroupFavicons.Click += OnMenuDownloadGroupFavicons;
            gcm.Items.Add(menuDownloadGroupFavicons);

            // Add a seperator and menu item to the entry context menu
            var ecm = m_host.MainWindow.EntryContextMenu;
            m_tsSeparator3 = new ToolStripSeparator();
            ecm.Items.Add(m_tsSeparator3);
            menuDownloadEntryFavicons = new ToolStripMenuItem();
            menuDownloadEntryFavicons.Text = "Download Favicons";
            menuDownloadEntryFavicons.Click += OnMenuDownloadEntryFavicons;
            ecm.Items.Add(menuDownloadEntryFavicons);

			return true; // Initialization successful
		}

		public override void Terminate()
		{
			// Remove 'Tools' menu items
			ToolStripItemCollection tsMenu = m_host.MainWindow.ToolsMenu.DropDownItems;
			tsMenu.Remove(m_tsSeparator1);
            tsMenu.Remove(menuDownloadFavicons);

            // Remove group context menu items
            var gcm = m_host.MainWindow.GroupContextMenu;
            gcm.Items.Remove(m_tsSeparator2);
            gcm.Items.Remove(menuDownloadGroupFavicons);

            // Remove entry context menu items
            var ecm = m_host.MainWindow.EntryContextMenu;
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
			if(!m_host.Database.IsOpen)
			{
				MessageBox.Show("Please open a database first.", "Favicon downloader");
				return;
			}

            KeePassLib.Collections.PwObjectList<PwEntry> output;
            output = m_host.Database.RootGroup.GetEntries(true);

            var progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Favicons", true, false, m_host.MainWindow);
            progressForm.Show();
            progressForm.StartLogging(null, false);
            progressForm.SetProgress(0);

            float progress = 0;
            float outputLength = (float)output.UCount;
            int downloadsCompleted = 0;

            foreach (PwEntry pwe in output)
            {
                downloadOneFavicon(pwe);
                downloadsCompleted++;
                progress = (downloadsCompleted / outputLength) * 100;
                progressForm.SetProgress((uint)Math.Floor(progress));
                System.Threading.Thread.Sleep(100);
                if (progressForm.UserCancelled)
                    break;
            }

            progressForm.Close();
            progressForm.Dispose();

            m_host.MainWindow.UpdateUI(false, null, false, null,
                true, null, true);
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

            var progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Favicons", true, false, m_host.MainWindow);
            progressForm.Show();
            progressForm.StartLogging(null, false);
            progressForm.SetProgress(0);

            float progress = 0;
            float outputLength = (float)pg.Entries.UCount;
            int downloadsCompleted = 0;

            foreach (PwEntry pwe in pg.Entries)
            {
                downloadOneFavicon(pwe);
                downloadsCompleted++;
                progress = (downloadsCompleted / outputLength) * 100;
                progressForm.SetProgress((uint)Math.Floor(progress));
                System.Threading.Thread.Sleep(100);
                if (progressForm.UserCancelled)
                    break;
            }

            progressForm.Close();
            progressForm.Dispose();

            m_host.MainWindow.UpdateUI(false, null, false, null,
                true, null, true);
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

            var progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Favicons", true, false, m_host.MainWindow);
            progressForm.Show();
            progressForm.StartLogging(null, false);
            progressForm.SetProgress(0);

            float progress = 0;
            float outputLength = (float)pwes.Length;
            int downloadsCompleted = 0;

            foreach (PwEntry pwe in pwes)
            {
                downloadOneFavicon(pwe);
                downloadsCompleted++;
                progress = (downloadsCompleted / outputLength) * 100;
                progressForm.SetProgress((uint)Math.Floor(progress));
                System.Threading.Thread.Sleep(100);
                if (progressForm.UserCancelled)
                    break;
            }

            progressForm.Close();
            progressForm.Dispose();

            m_host.MainWindow.UpdateUI(false, null, false, null,
                true, null, true);
        }

        /// <summary>
        /// Downloads one favicon and attaches it to the entry
        /// </summary>
        /// <param name="pwe">The entry for which we want to download the favicon</param>
        private void downloadOneFavicon(PwEntry pwe)
        {
            // TODO: create async jobs instead?

            string url = pwe.Strings.ReadSafe("URL");
            
            // If we have no URL, quit
            if (string.IsNullOrEmpty(url))
                return;

            // If we have a URL with specific scheme that is not http or https, quit
            if (!url.StartsWith("http://") && !url.StartsWith("https://")
                && url.Contains("://"))
                return;

            int dotIndex = url.IndexOf(".");
            if (dotIndex >= 0)
            {
                // trim any path data
                int slashDotIndex = url.IndexOf("/", dotIndex);
                if (slashDotIndex >= 0)
                    url = url.Substring(0, slashDotIndex);

                // If there is a protocol/scheme prepended to the URL, strip it off.
                int protocolEndIndex = url.LastIndexOf("/");
                if (protocolEndIndex >= 0)
                    url = url.Substring(protocolEndIndex + 1);

                //WebRequest webreq = WebRequest.Create("http://getfavicon.appspot.com/http://"+url); // 500 internal server error
                WebRequest webreq = WebRequest.Create("http://www.faviconiac.com/favicon/" + url); // lots missing, although they seem to appear a few days after first request...
                //WebRequest webreq = WebRequest.Create("http://www.getfavicon.org/?url=" + url.Substring(url.LastIndexOf("/") + 1) + "/favicon.png"); // timed out
                // Google S2 - favicon.ico only

                WebResponse response = webreq.GetResponse();
                Stream s = response.GetResponseStream();
                Image img = null;
                MemoryStream memStream = new MemoryStream();

                byte[] respBuffer = new byte[40960];
                try
                {
                    int bytesRead = s.Read(respBuffer, 0, respBuffer.Length);
                    if (bytesRead > 0)
                    {
                        memStream.Write(respBuffer, 0, bytesRead);
                        bytesRead = s.Read(respBuffer, 0, respBuffer.Length);
                    }
                }
                finally
                {
                    s.Close();
                }

                try { img = Image.FromStream(memStream); }
                catch (Exception)
                {
                    try { img = (new Icon(s)).ToBitmap(); }
                    catch (Exception) { throw; }
                }

                var webResponseByteArray = memStream.ToArray();
                string currentImageData = Convert.ToBase64String(webResponseByteArray);

                Image imgNew = new Bitmap(img, new Size(16, 16));

                MemoryStream ms = new MemoryStream();
                imgNew.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                // ignore the default image (don't change existing entry icon)
                if (currentImageData != defaultImage)
                {
                    var msByteArray = ms.ToArray();

                    foreach (var item in m_host.Database.CustomIcons)
                    {
                        // re-use existing custom icon if it's already in the database
                        // (This will probably fail if database is used on 
                        // both 32 bit and 64 bit machines - not sure why...)
                        if (KeePassLib.Utility.MemUtil.ArraysEqual(msByteArray, item.ImageDataPng))
                        {
                            pwe.CustomIconUuid = item.Uuid;
                            m_host.Database.UINeedsIconUpdate = true;
                            return;
                        }
                    }

                    // Create a new custom icon for use with this entry
                    PwCustomIcon pwci = new PwCustomIcon(new PwUuid(true),
                        ms.ToArray());
                    m_host.Database.CustomIcons.Add(pwci);
                    pwe.CustomIconUuid = pwci.Uuid;

                    m_host.Database.UINeedsIconUpdate = true;
                }

            }

        }

	}
}
