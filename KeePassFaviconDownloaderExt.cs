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
using KeePassLib.Interfaces;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace KeePassFaviconDownloader
{
    public sealed class KeePassFaviconDownloaderExt : Plugin
    {
        // The plugin remembers its host in this variable.
        private IPluginHost m_host = null;
        private static SecurityProtocolType originalSecurityProtocol = System.Net.ServicePointManager.SecurityProtocol;

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
            StatusProgressForm progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Favicons", true, false, m_host.MainWindow);
            progressForm.Show();
            progressForm.SetProgress(0);

            float progress = 0;
            float outputLength = (float)entries.UCount;
            int downloadsCompleted = 0;
            string errorMessage = "";
            int errorCount = 0;

            foreach (PwEntry pwe in entries)
            {
                string message = "";

                progressForm.SetText("Title: " + pwe.Strings.ReadSafe("Title") + "; User Name: " + pwe.Strings.ReadSafe("UserName"), LogStatusType.Info);

                downloadOneFavicon(pwe, ref message);
                if (message != "")
                {
                    errorMessage = "For an entry with URL '" + pwe.Strings.ReadSafe("URL") + "': " + message;
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

            m_host.MainWindow.UpdateUI(false, null, false, null,
                true, null, true);
            m_host.MainWindow.UpdateTrayIcon();
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
                Uri fullURI = null;
                try
                {
                    fullURI = new Uri((url.StartsWith("http://") || url.StartsWith("https://")) ? url : "http://" + url, UriKind.Absolute);
                }
                catch (Exception ex)
                {
                    message += url + "\n" + ex.Message;
                    return;
                }

                MemoryStream ms = null;
                Uri lastURI = getFromFaviconExplicitLocation(fullURI, ref ms, ref message);
                bool success = (lastURI != null) && lastURI.OriginalString.Equals("http://success");

                if (!success)
                {
                    success = getFavicon(new Uri((lastURI == null) ? fullURI : lastURI, "/favicon.ico"), ref ms, ref message);
                }

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

            return new Uri(uri, redirect);
        }

        bool PreRequest_EventHandler(HttpWebRequest request)
        {
            request.CookieContainer = new System.Net.CookieContainer();
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.Headers.Add(HttpRequestHeader.AcceptLanguage, "*");
            return true;
        }

        /// <summary>
        /// Gets a memory stream representing an image from an explicit favicon location.
        /// </summary>
        /// <param name="fullURI">The URI.</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns></returns>
        private Uri getFromFaviconExplicitLocation(Uri fullURI, ref MemoryStream ms, ref string message)
        {
            HtmlWeb hw = new HtmlWeb();
            hw.UserAgent = "Mozilla/5.0 (Windows 6.1; rv:27.0) Gecko/20100101 Firefox/27.0";
            HtmlAgilityPack.HtmlDocument hdoc = null;
            Uri responseURI = null;

            try
            {
                int counter = 0; // Protection from cyclic redirect 
                Uri nextUri = fullURI;
                do
                {
                    // A cookie container is needed for some sites to work
                    hw.PreRequest += PreRequest_EventHandler;

                    // HtmlWeb.Load will follow 302 and 302 redirects to alternate URIs
                    hdoc = hw.Load(nextUri.AbsoluteUri);
                    responseURI = hw.ResponseUri;

                    // Old school meta refreshes need to parsed
                    nextUri = getMetaRefreshLink(responseURI, hdoc);
                    counter++;
                } while (nextUri != null && counter < 16); // Sixteen redirects would be more than enough.
            }
            catch (Exception)
            {
                return responseURI;
            }

            if (hdoc == null)
                return responseURI;

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
                        string val = r.Value.ToLower().Replace("shortcut", "").Trim();
                        if (val == "icon")
                        {
                            try
                            {
                                faviconLocation = node.Attributes["href"].Value;
                                // Don't break the loop, because there could be many <link rel="icon"> nodes
                                // We should read the last one, like web browsers do
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
            if (String.IsNullOrEmpty(faviconLocation))
            {
                return responseURI;
            }

            return (getFavicon(new Uri(responseURI, faviconLocation), ref ms, ref message)) ? new Uri("http://success") : responseURI;
        }

        /// <summary>
        /// Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns></returns>
        private bool getFavicon(Uri uri, ref MemoryStream ms, ref string message)
        {
            return getFaviconWithSecurityProtocol(uri, ref ms, ref message, originalSecurityProtocol) ||
                (originalSecurityProtocol != SecurityProtocolTypeExtensions.Tls12 && getFaviconWithSecurityProtocol(uri, ref ms, ref message, SecurityProtocolTypeExtensions.Tls12)) ||
                (originalSecurityProtocol != SecurityProtocolTypeExtensions.Tls11 && getFaviconWithSecurityProtocol(uri, ref ms, ref message, SecurityProtocolTypeExtensions.Tls11));
        }

        private bool getFaviconWithSecurityProtocol(Uri uri, ref MemoryStream ms, ref string message, SecurityProtocolType securityProtocolType)
        {
            Stream s = null;
            Image img = null;
            MemoryStream memStream = new MemoryStream();

            try
            {
                ServicePointManager.SecurityProtocol = securityProtocolType;

                WebRequest webreq = WebRequest.Create(uri);
                ((HttpWebRequest)webreq).UserAgent = "Mozilla/5.0 (Windows 6.1; rv:27.0) Gecko/20100101 Firefox/27.0";
                ((HttpWebRequest)webreq).CookieContainer = new System.Net.CookieContainer();
                ((HttpWebRequest)webreq).Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                ((HttpWebRequest)webreq).Headers.Add(HttpRequestHeader.AcceptLanguage, "*");
                webreq.Timeout = 10000; // don't think it's expecting too much for a few KB to be delivered inside 10 seconds.

                WebResponse response = webreq.GetResponse();

                if (response == null)
                {
                    message += "Could not download favicon(s). This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n - No response from server";
                    return false;
                }
                if (uri != response.ResponseUri)
                {
                    //Redirect ?
                    return getFavicon(response.ResponseUri, ref ms, ref message);
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

                try
                {
                    Icon icon = new Icon(memStream);
                    icon = new Icon(icon, 16, 16);
                    img = icon.ToBitmap();
                }
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
                message += "Could not download favicon(s). This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n" + webException.Status + ": " + webException.Message + ": " + webException.Response;
                if (s != null)
                    s.Close();
                return false;
            }
            catch (Exception generalException)
            {
                // don't show this everytime a website has an invalid favicon - it could get old fast.
                message += "Could not download favicon(s). This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n" + generalException.Message + ".";
                if (s != null)
                    s.Close();
                return false;
            }

            try
            {
                Bitmap imgNew = new Bitmap(16, 16);
                imgNew.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                using (Graphics g = Graphics.FromImage(imgNew))
                {
                    // set the resize quality modes to high quality
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.DrawImage(img, 0, 0, imgNew.Width, imgNew.Height);
                }
                ms = new MemoryStream();
                imgNew.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return true;
            }
            catch (Exception ex)
            {
                message += "Could not process downloaded favicon. This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n" + ex.Message + ".";
                if (s != null)
                    s.Close();
                return false;
            }
        }
    }
}