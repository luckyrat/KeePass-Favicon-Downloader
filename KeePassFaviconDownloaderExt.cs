/*
  KeePass Favicon Downloader - KeePass plugin that downloads and stores
  favicons for entries with web URLs.
  Copyright (C) 2009-2011 Chris Tomlinson <luckyrat@users.sourceforge.net>
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
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace KeePassFaviconDownloader
{
	public sealed class KeePassFaviconDownloaderExt : Plugin
	{
		// The plugin remembers its host in this variable.
		private IPluginHost m_host = null;

        public override string UpdateUrl { get { return "https://raw.github.com/luckyrat/KeePass-Favicon-Downloader/master/versionInfo.txt"; } }

        private ToolStripSeparator m_tsSeparator1 = null;
        private ToolStripSeparator m_tsSeparator2 = null;
        private ToolStripSeparator m_tsSeparator3 = null;
        private ToolStripMenuItem menuDownloadFavicons = null;
        private ToolStripMenuItem menuDownloadGroupFavicons = null;
        private ToolStripMenuItem menuDownloadEntryFavicons = null;

        private BackgroundWorker worker = null;
        private StatusProgressForm progressForm = null;

        /// <summary>
        /// Initializes the plugin using the specified KeePass host.
        /// </summary>
        /// <param name="host">The plugin host.</param>
        /// <returns></returns>
		public override bool Initialize(IPluginHost host)
		{
			Debug.Assert(host != null);
			if(host == null) return false;
			m_host = host;

			// Add a separator and menu item to the 'Tools' menu
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

            // Initialize the background worker
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DownloadFavicons;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;

			return true; // Initialization successful
		}

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.m_host.MainWindow.Enabled = true;
            progressForm.Hide();
            progressForm.Close();
            progressForm = null;
            if (!e.Cancelled)
            {
                var errorMessages = (List<string>)e.Result;
                if (errorMessages.Count > 0)
                {
                    MessageBox.Show(errorMessages.Count + " errors occurred. The last 5 error messages are shown here.\n" +
                                                    "To see the other messages, select a smaller group of entries and use the right click menu to start the download.\n"
                                                    + String.Join("\n", errorMessages.ToArray(), 0, Math.Min(5, errorMessages.Count)), "Download errors");
                }
            }
            else
            {
                MessageBox.Show("Favicon download cancelled","Operation cancelled",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }

            m_host.MainWindow.UpdateUI(false, null, false, null,
                true, null, true);
            m_host.MainWindow.UpdateTrayIcon();
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressForm.SetProgress((uint)e.ProgressPercentage);
        }

        void worker_DownloadFavicons(object sender, DoWorkEventArgs e)
        {
            var entries = (KeePassLib.Collections.PwObjectList<PwEntry>)e.Argument;
            int progress = 0;
            uint downloadsCompleted = 0;
            var errorMessages = new List<string>();

            foreach (PwEntry pwe in entries)
            {
                string errorMessage = "";
                downloadOneFavicon(pwe, ref errorMessage);
                if (errorMessage != "")
                {
                    errorMessages.Add(errorMessage);
                }

                downloadsCompleted++;
                progress = (int)(100 * downloadsCompleted / entries.UCount);
                worker.ReportProgress(progress);
                System.Threading.Thread.Sleep(100);
                if (worker.CancellationPending || progressForm.UserCancelled) {
                    e.Cancel = true;
                    break;
                };
            }

            e.Result = errorMessages;
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
			if(!m_host.Database.IsOpen)
			{
				MessageBox.Show("Please open a database first.", "Favicon downloader");
				return;
			}

            KeePassLib.Collections.PwObjectList<PwEntry> output;
            output = m_host.Database.RootGroup.GetEntries(true);
            downloadSomeFavicons(output);  
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
            downloadSomeFavicons(pg.Entries);  
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
            downloadSomeFavicons(KeePassLib.Collections.PwObjectList<PwEntry>.FromArray(pwes));            
        }

        /// <summary>
        /// Downloads some favicons.
        /// </summary>
        /// <param name="entries">The entries.</param>
        private void downloadSomeFavicons(KeePassLib.Collections.PwObjectList<PwEntry> entries)
        {
            progressForm = new StatusProgressForm();
            this.m_host.MainWindow.Enabled = false;

            progressForm.InitEx("Downloading Favicons", true, false, m_host.MainWindow);
            progressForm.Show();
            progressForm.SetProgress(0);
            worker.RunWorkerAsync(entries);
        }

        /// <summary>
        /// Downloads one favicon and attaches it to the entry
        /// </summary>
        /// <param name="pwe">The entry for which we want to download the favicon</param>
        private void downloadOneFavicon(PwEntry pwe, ref string message)
        {
            // TODO: create async jobs instead?

            string url = pwe.Strings.ReadSafe("URL");

            if (string.IsNullOrEmpty(url))
                url = pwe.Strings.ReadSafe("Title");
            
            // If we still have no URL, quit
            if (string.IsNullOrEmpty(url))
                return;

            // If we have a URL with specific scheme that is not http or https, quit
            if (!url.StartsWith("http://") && !url.StartsWith("https://")
                && url.Contains("://"))
                return;

            int dotIndex = url.IndexOf(".");
            if (dotIndex >= 0)
            {
                string protocol = "http";
                string fullURL = url;

                // trim any path data
                int slashDotIndex = url.IndexOf("/", dotIndex);
                if (slashDotIndex >= 0)
                    url = url.Substring(0, slashDotIndex);

                // If there is a protocol/scheme prepended to the URL, strip it off.
                int protocolEndIndex = url.LastIndexOf("/");
                if (protocolEndIndex >= 0)
                {
                    protocol = url.Substring(0,protocolEndIndex-2);
                    url = url.Substring(protocolEndIndex + 1);
                }

                MemoryStream ms = null;
                bool success = getFromFaviconExplicitLocation(url, protocol, fullURL, ref ms, ref message);

                if (!success)
                    success = getFromFaviconStandardLocation(url, protocol, ref ms, ref message);

                if (!success)
                    return;

                // If we found an icon then we don't care whether one particular download method failed.
                message = "";

                byte[] msByteArray = ms.ToArray();

                foreach (PwCustomIcon item in m_host.Database.CustomIcons)
                {
                    // re-use existing custom icon if it's already in the database
                    // (This will probably fail if database is used on 
                    // both 32 bit and 64 bit machines - not sure why...)
                    if (KeePassLib.Utility.MemUtil.ArraysEqual(msByteArray, item.ImageDataPng))
                    {
                        pwe.CustomIconUuid = item.Uuid;
                        pwe.Touch(true);
                        m_host.Database.UINeedsIconUpdate = true;
                        return;
                    }
                }

                // Create a new custom icon for use with this entry
                PwCustomIcon pwci = new PwCustomIcon(new PwUuid(true),
                    ms.ToArray());
                m_host.Database.CustomIcons.Add(pwci);
                pwe.CustomIconUuid = pwci.Uuid;
                pwe.Touch(true);
                m_host.Database.UINeedsIconUpdate = true;
            }

        }
        private Uri reconcileURI(Uri baseUri, string newUri)
        {
            // If there is nothing new, then return the original Uri
            if (String.IsNullOrEmpty(newUri))
            {
                return baseUri;
            }

            // If the newURI is a full URI, then return that, otherwise we'll get a UriFormatException
            try
            {
                return new Uri(newUri);
            }
            catch (Exception) { }

            return new Uri(baseUri, newUri);
        }

        private Uri getMetaRefreshLink(Uri uri, HtmlAgilityPack.HtmlDocument hdoc)
        {
            HtmlNodeCollection metas = hdoc.DocumentNode.SelectNodes("/html/head/meta");
            string redirect = null;

            if (metas == null)
            {
                return null;
            }

            for (int i = 0; i < metas.Count; i++)
            {
                HtmlNode node = metas[i];
                try
                {
                    HtmlAttribute httpeq = node.Attributes["http-equiv"];
                    HtmlAttribute content = node.Attributes["content"];
                    if (httpeq.Value.ToLower().Equals("location") || httpeq.Value.ToLower().Equals("refresh"))
                    {
                        if (content.Value.ToLower().Contains("url"))
                        {
                            Match match = Regex.Match(content.Value.ToLower(), @".*?url[\s=]*(\S+)");
                            if (match.Success)
                            {
                                redirect = match.Captures[0].ToString();
                                redirect = match.Groups[1].ToString();
                            }
                        }

                    }
                }
                catch (Exception) { }
            }

            if (String.IsNullOrEmpty(redirect))
            {
                return null;
            }

            return reconcileURI(uri, redirect);
        }

        /// <summary>
        /// Gets a memory stream representing an image from an explicit favicon location.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="protocol">i.e http or https</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns></returns>
        private bool getFromFaviconExplicitLocation(string url, string protocol, string fullURL, ref MemoryStream ms, ref string message)
        {
            if (protocol != "https")
                protocol = "http";
                        
            HtmlWeb hw = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument hdoc = null;
            Uri responseURI = null; 

            try
            {
                int counter = 0; // Protection from cyclic redirect
                Uri nextUri = new Uri(fullURL);
                do
                {
                    // HtmlWeb.Load will follow 302 and 302 redirects to alternate URIs
                    hdoc = hw.Load(nextUri.AbsoluteUri);
                    responseURI = hw.ResponseUri;

                    // Old school meta refreshes need to parsed
                    nextUri = getMetaRefreshLink(responseURI, hdoc);
                    counter++; // Protection from cyclic redirect

                } while (nextUri != null && counter<16); // Sixteen redirects would be more than enough.


            }
            catch (Exception)
            {
                return false;
            }

            if (hdoc == null)
                return false;

            string faviconLocation = "";
            try
            {
                HtmlNodeCollection links = hdoc.DocumentNode.SelectNodes("/html/head/link");
                for (int i = 0; i < links.Count; i++)
                {
                    HtmlNode node = links[i];
                    try
                    {
                        HtmlAttribute r = node.Attributes["rel"];
                        if (r.Value.ToLower().CompareTo("shortcut icon") == 0 || r.Value.ToLower().CompareTo("icon") == 0)
                        {
                            try
                            {
                                faviconLocation = node.Attributes["href"].Value;
                                break;
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrEmpty(faviconLocation))
                return false;

            faviconLocation = reconcileURI(responseURI, faviconLocation).AbsoluteUri; 
            return getFavicon(faviconLocation, ref ms, ref message);

        }

        /// <summary>
        /// Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="protocol">i.e http or https</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns></returns>
        private bool getFromFaviconStandardLocation(string url, string protocol, ref MemoryStream ms, ref string message)
        {
            if (protocol != "https")
                protocol = "http";

            return getFavicon(protocol + "://" + url + "/favicon.ico", ref ms, ref message);

        }

        /// <summary>
        /// Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="protocol">i.e http or https</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns></returns>
        private bool getFavicon(string url, ref MemoryStream ms, ref string message)
        {
            Stream s = null;
            Image img = null;
            MemoryStream memStream = new MemoryStream();

            const string errMessage = "";

            try
            {
                WebRequest webreq = WebRequest.Create(url);
                webreq.Timeout = 10000; // don't think it's expecting too much for a few KB to be delivered inside 10 seconds.

                WebResponse response = webreq.GetResponse();
                
                if( response==null )
                {
                    message += errMessage + "No response from server";
                    return false;
                }
                if( string.Compare(response.ResponseUri.ToString(), url, StringComparison.InvariantCultureIgnoreCase) != 0 )
                {
                    //Redirect ?
                    return getFavicon(response.ResponseUri.ToString(), ref ms, ref message);
                }

                s = response.GetResponseStream();

                int count = 0;
                byte[] buffer = new byte[4097];
                do
                {
                    count = s.Read(buffer, 0, buffer.Length);        
                    memStream.Write(buffer, 0, count);    
                    if (count == 0)
                        break;
                }    
                while (true);
                memStream.Position = 0;
                
                // END change

                try { img = (new Icon(memStream)).ToBitmap(); }
                catch (Exception)
                {
                    // This shouldn't be useful unless someone has messed up their favicon format
                    try { img = Image.FromStream(memStream); } 
                    catch (Exception) { throw; }
                }

            }
            catch (WebException webException)
            {
                // don't show this everytime a website has a missing favicon - it could get old fast.
                message += errMessage + webException.Status + ": " + webException.Message + ": " + webException.Response;
                if (s != null)
                    s.Close();
                return false;
            }
            catch (Exception generalException)
            {
                // don't show this everytime a website has an invalid favicon - it could get old fast.
                message += errMessage + generalException.Message + ".";
                if (s != null)
                    s.Close();
                return false;
            }

            try
            {
                Image imgNew = new Bitmap(img, new Size(16, 16));

                ms = new MemoryStream();
                imgNew.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return true;
            }
            catch (Exception ex)
            {
                message += "Could not process downloaded favicon. " + ex.Message + ".";
                if (s != null)
                    s.Close();
                return false;
            }
        }

	}
}
