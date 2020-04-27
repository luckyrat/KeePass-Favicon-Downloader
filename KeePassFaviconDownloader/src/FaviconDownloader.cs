using System;
using System.IO;
using System.Drawing;

using KeePassLib.Utility;

using HtmlAgilityPack;

namespace KeePassFaviconDownloader
{
    public static class FaviconDownloader
    {
        /// <summary>
        /// Gets a memory stream representing an image from a fuzzy favicon location.
        /// </summary>
        /// <param name="url">The URL to fuzzy download favicons for.</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns>Favicon memory stream.</returns>
        public static MemoryStream GetFuzzyForWebsite(string url, ref string message)
        {
            MemoryStream ms = null;

            // If we have a URL with specific protocol that is not http or https, quit
            if (!url.StartsWith("http://", StringComparison.CurrentCulture) && !url.StartsWith("https://", StringComparison.CurrentCulture))
            {
                if (url.Contains("://"))
                { // NOTE URI standard only requires ":", but it should be differentiated to port separator "domain:port"
                    message += "Invalid URL (unsupported protocol): " + url;
                    return null;
                }
                else
                {
                    url = "http://" + url;
                }
            }

            // Try to create an URI
            Uri fullURI = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out fullURI))
            {
                message += "Invalid URI: " + url;
                return null;
            }

            // Parse website and try to load favicon
            Uri lastUri = fullURI;  // save location after redirects
            ms = GetForWebsite(ref lastUri, ref message);

            // Guess location based on (redirected) hostname
            if (ms == null)
            {
                message += "\n";
                ms = GetFavicon(new Uri(lastUri, "/favicon.ico"), ref message);
            }

            // Swap scheme of original URI
            if (ms == null)
            {
                message += "\n";

                UriBuilder uriBuilder = new UriBuilder(fullURI);
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
                lastUri = uriBuilder.Uri;
                ms = GetForWebsite(ref lastUri, ref message);
            }

            return ms;

        }

        /// <summary>
        /// Gets a memory stream representing an image from an explicit favicon location.
        /// </summary>
        /// <param name="fullURI">The URI (will be updated on redirects).</param>
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns>The memory stream (output).</returns>
        public static MemoryStream GetForWebsite(ref Uri fullURI, ref string message)
        {
            // Download and parse HTML
            HtmlAgilityPack.HtmlDocument hdoc = null;
            try
            {
                hdoc = FaviconConnection.GetHtmlDocumentFollowMeta(ref fullURI);
                if (hdoc == null)
                {
                    message += "Could not read website " + fullURI;
                    return null;
                }

                string faviconLocation = "";
                HtmlNodeCollection links = hdoc.DocumentNode.SelectNodes("/html/head/link");
                foreach (HtmlNode node in links)
                {
                    string val = node.Attributes["rel"]?.Value.ToLower().Replace("shortcut", "").Trim();
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

                return GetFavicon(new Uri(fullURI, faviconLocation), ref message);
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
        /// <param name="message">Any error message is sent back through this string.</param>
        /// <returns>The memory stream (output).</returns>
        public static MemoryStream GetFavicon(Uri uri, ref string message)
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
                    return null;
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
            catch (Exception ex)
            {
                message += "Could not process downloaded favicon from " + uri.AbsoluteUri + ":\n" + ex.Message + ".";
                return null;
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
                MemoryStream ms = new MemoryStream();
                imgNew.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms;
            }
            catch (Exception ex)
            {
                message += "Could not resize downloaded favicon from " + uri.AbsoluteUri + ":\n" + ex.Message + ".";
                return null;
            }
        }
    }
}
