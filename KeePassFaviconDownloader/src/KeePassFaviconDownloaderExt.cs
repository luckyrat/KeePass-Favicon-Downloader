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
using KeePassLib.Serialization;
using KeePassLib.Utility;

using HtmlAgilityPack;
using System.Text.RegularExpressions;

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

                progressForm.SetText("Title: " + pwe.Strings.ReadSafe("Title") + "; User Name: " + pwe.Strings.ReadSafe("UserName"),LogStatusType.Info);

                downloadOneFavicon(pwe, ref message);
                if (message != "")
                {
                    errorMessage = "For an entry with URL '"+pwe.Strings.ReadSafe("URL")+"':\n" + message;
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
        private void downloadOneFavicon(PwEntry pwe, ref string message)
        {
            // TODO: create async jobs instead?

            // Read URL
            string url = pwe.Strings.ReadSafe("URL");

            // Use title of no URL is given
            if (string.IsNullOrEmpty(url))
                url = pwe.Strings.ReadSafe("Title");

            // If we still have no URL, quit
            if (string.IsNullOrEmpty(url))
                return;

            // If we have a URL with specific protocol that is not http or https, quit
            if (!url.StartsWith("http://", StringComparison.CurrentCulture) && !url.StartsWith("https://", StringComparison.CurrentCulture))
            {
                if (url.Contains("://")) { // NOTE URI standard only requires ":", but it should be differentiated to port separator "domain:port"
                    message += "Invalid URL (unsupported protocol): " + url;
                    return;
                } else {
                    url = "http://" + url;
                }
            }

            // Try to create an URI
            Uri fullURI = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out fullURI))
            {
                message += "Invalid URI: " + url;
                return;
            }

            MemoryStream ms = null;
            Uri lastURI = getFromFaviconExplicitLocation(fullURI, ref ms, ref message) ?? fullURI;
            bool success = lastURI.OriginalString.Equals("http://success");
            // TODO no reason to continue for WebException.Status: NameResolutionFailure, TrustFailure (SSL), ...

            // Swap scheme
            if (!success)
            {
                message += "\n";

                UriBuilder uriBuilder = new UriBuilder(lastURI);
                if (uriBuilder.Scheme.Equals("http"))
                {
                    uriBuilder.Scheme = "https";
                    uriBuilder.Port = 443;
                }
                else
                {
                    uriBuilder.Scheme = "http";
                    uriBuilder.Port = 80;
                }
                lastURI = getFromFaviconExplicitLocation(uriBuilder.Uri, ref ms, ref message) ?? fullURI;
                success = lastURI.OriginalString.Equals("http://success");
            }

            // Guess location
            if (!success)
            {
                message += "\n";

                UriBuilder uriBuilder = new UriBuilder(fullURI.Scheme, fullURI.Host, fullURI.Port, "favicon.ico");
                success = getFavicon(uriBuilder.Uri, ref ms, ref message);
            }

            if (!success)
            {
                return;
            }

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

        /// <summary>
        /// Gets a memory stream representing an image from an explicit favicon location.
        /// </summary>
        /// <param name="fullURI">The URI.</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns>URI after redirect or null on error.</returns>
        private Uri getFromFaviconExplicitLocation(Uri fullURI, ref MemoryStream ms, ref string message)
        {
            // Download website
            Stream stream = null;
            string html = "";
            try
            {
                // Open
                HttpWebResponse response = FaviconConnection.GetResponse(fullURI);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    message += "Could not download website: " + response.StatusDescription;
                    return null;
                }
                // Use actually used URI (respects redirects)
                // - Respects HTTP redirects
                // - Ignores HTML meta or Javascript redirects (v1.9.0 supports meta)
                fullURI = response.ResponseUri;
                // Get response stream
                stream = response.GetResponseStream();

                // Read
                StreamReader streamReader = new StreamReader(stream);
                html = streamReader.ReadToEnd();
                if (String.IsNullOrEmpty(html))
                {
                    message += "Could not download website: Empty response.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                message += "Could not download website: " + ex.Message;
                return null;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            // Parse HTML
            HtmlAgilityPack.HtmlDocument hdoc = null;
            try
            {
                hdoc = new HtmlAgilityPack.HtmlDocument();
                hdoc.LoadHtml(html);
                if (hdoc == null)
                {
                    message += "Could not read website.";
                    return null;
                }

                string faviconLocation = "";
                HtmlNodeCollection links = hdoc.DocumentNode.SelectNodes("/html/head/link");
                foreach (HtmlNode node in links)
                {
                    string val = node.Attributes["rel"]?.Value.ToLower().Replace("shortcut","").Trim();
                    if (val == "icon")
                    {
                        faviconLocation = node.Attributes["href"]?.Value;
                        // Don't break the loop, because there could be many <link rel="icon"> nodes
                        // We should read the last one, like web browsers do
                    }
                }
                if (String.IsNullOrEmpty(faviconLocation))
                {
                    message += "Could not find favicon link within website.";
                    return null;
                }

                return (getFavicon(new Uri(fullURI, faviconLocation), ref ms, ref message))?new Uri("http://success"):null;
            }
            catch (Exception ex)
            {
                message += "Could not parse website: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="ms">The memory stream (output).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns>True when successful.</returns>
        private bool getFavicon(Uri uri, ref MemoryStream ms, ref string message)
        {
            Image img = null;

            try
            {
                Stream stream = null;
                MemoryStream memoryStream = new MemoryStream();

                stream = FaviconConnection.OpenRead(uri);
                if (stream == null)
                {
                    message += "Could not download favicon from " + uri.AbsoluteUri + ":\nNo or empty response.";
                    return false;
                }

                MemUtil.CopyStream(stream, memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                try
                {
                    Icon icon = new Icon(memoryStream);
                    icon = new Icon(icon, 16, 16);
                    img = icon.ToBitmap();
                }
                catch (Exception)
                {
                    // This shouldn't be useful unless someone has messed up their favicon format
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    try
                    {
                        // This expects the stream to contain ONLY one image and nothing else
                        img = Image.FromStream(memoryStream);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Invalid image format: " + ex.Message);
                    }
                }
                finally
                {
                    // TODO The MemoryStream has to remain open as long as Image.FromStream is in use!
                    if (memoryStream != null)
                        memoryStream.Close();
                    if (stream != null)
                        stream.Close();
                }

            }
            catch (WebException webException)
            {
                // WebExceptionStatus: https://docs.microsoft.com/en-us/dotnet/api/system.net.webexceptionstatus?view=netframework-2.0
                //   WebExceptionStatus status = webException.Status;
                // for status == WebExceptionStatus.ProtocolError
                //   ((HttpWebResponse)webException.Response).StatusDescription;
                //   ((HttpWebResponse)webException.Response).StatusCode;
                message += "Could not download favicon from " + uri.AbsoluteUri + ":\n" + webException.Message;
                return false;
            }
            catch (Exception ex)
            {
                message += "Could not process downloaded favicon from " + uri.AbsoluteUri + ":\n" + ex.Message + ".";
                return false;
            }

            try
            {
                Bitmap imgNew = new Bitmap(16, 16);
                if (img.HorizontalResolution > 0 && img.VerticalResolution > 0)
                {
                    imgNew.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                }
                else
                {
                    imgNew.SetResolution(72, 72);
                }
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
                message += "Could not resize downloaded favicon from " + uri.AbsoluteUri + ":\n" + ex.Message + ".";
                return false;
            }
        }
    }
}
