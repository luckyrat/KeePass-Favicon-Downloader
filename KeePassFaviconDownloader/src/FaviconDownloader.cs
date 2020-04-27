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
        /// <returns>Favicon memory stream.</returns>
        public static MemoryStream GetFuzzyForWebsite(string url)
        {
            // If we have a URL with specific protocol that is not http or https, quit
            if (!url.StartsWith("http://", StringComparison.CurrentCulture) && !url.StartsWith("https://", StringComparison.CurrentCulture))
            {
                if (url.Contains("://"))
                { // NOTE URI standard only requires ":", but it should be differentiated to port separator "domain:port"
                    throw new FormatException("Invalid URI (unsupported protocol): " + url);
                }
                else
                {
                    url = "http://" + url;
                }
            }

            // Try to create an URI
            string errorMessage = "";  // TODO generate automatically in multi-inner-exception
            Uri fullURI = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out fullURI))
            {
                throw new FormatException("Invalid URI: " + url);
            }

            // Parse website and try to load favicon
            Uri lastUri = fullURI;  // save location after redirects
            try
            {
                return GetForWebsite(ref lastUri);
            }
            catch (Exception ex) { errorMessage += "\n" + ex.Message; }

            // Guess location based on (redirected) hostname
            try
            {
                return GetFavicon(new Uri(lastUri, "/favicon.ico"));
            }
            catch (Exception ex) { errorMessage += "\n" + ex.Message; }

            // Swap scheme of original URI
            try
            {
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
                return GetForWebsite(ref lastUri);
            }
            catch (Exception ex) { errorMessage += "\n" + ex.Message; }

            // None of our approaches returned something useful
            throw new Exception("Could not process favicon for " + url + ":" + errorMessage);

        }

        /// <summary>
        /// Gets a memory stream representing an image from an explicit favicon location.
        /// </summary>
        /// <param name="fullURI">The URI (will be updated on redirects).</param>
        /// <returns>The memory stream (output).</returns>
        public static MemoryStream GetForWebsite(ref Uri fullURI)
        {
            // Download and parse HTML
            string faviconLocation = "";
            try
            {
                HtmlAgilityPack.HtmlDocument hdoc = null;
                hdoc = FaviconConnection.GetHtmlDocumentFollowMeta(ref fullURI);
                if (hdoc == null)
                {
                    throw new Exception("Could not read website " + fullURI.AbsoluteUri + ":\nNo or empty response.");
                }

                // TODO prefer high-resolution apple-touch-icon
                // https://github.com/luckyrat/KeePass-Favicon-Downloader/issues/13
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
                    throw new Exception("Could not find valid favicon link within website.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Could not parse website.", ex);
            }

            return GetFavicon(new Uri(fullURI, faviconLocation));
        }

        /// <summary>
        /// Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The memory stream (output).</returns>
        public static MemoryStream GetFavicon(Uri uri)
        {
            Image img = null;
            MemoryStream memoryStream = new MemoryStream();
            try
            {
                // Download and create favicon image object
                try
                {
                    Stream stream = FaviconConnection.OpenRead(uri);
                    if (stream == null)
                    {
                        throw new Exception("Could not download favicon from " + uri.AbsoluteUri + ":\nNo or empty response.");
                    }

                    MemUtil.CopyStream(stream, memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    try
                    {
                        // TODO compressed PNG inside ICO file are not supported in mono
                        // https://github.com/picoe/Eto/issues/603
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
                            throw new Exception("Invalid image format for " + uri.AbsoluteUri + ".", ex);
                        }
                    }
                    finally
                    {
                        if (stream != null)
                            stream.Close();
                    }

                }
                catch (Exception ex)
                {
                    throw new Exception("Could not download and process favicon from " + uri.AbsoluteUri + ".", ex);
                }

                // Resize downloaded favicon
                // TODO support larger icons in newer KeePass versions
                // https://github.com/luckyrat/KeePass-Favicon-Downloader/issues/13
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
                    throw new Exception("Could not resize downloaded favicon from " + uri.AbsoluteUri + ".", ex);
                }
            }
            finally
            {
                // MemoryStream has to remain open as long as Image.FromStream is in use!
                if (memoryStream != null)
                    memoryStream.Close();
            }
        }
    }
}
