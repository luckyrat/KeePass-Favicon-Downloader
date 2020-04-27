using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using System.Net;
using System.Net.Cache;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using HtmlAgilityPack;

using KeePass;
using KeePassLib;
using KeePassLib.Utility;

// Based on KeePassLib.Serialization.IOConnection
namespace KeePassFaviconDownloader
{
    public sealed class FaviconWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            FaviconConnection.ConfigureWebRequest((HttpWebRequest)request);
            return request;
        }
    }

    public static class FaviconConnection
    {
        // This method should be called for *every* request
        // Here, all the request parameters are specified
        public static bool ConfigureWebRequest(HttpWebRequest request)
        {
            try
            {
                IWebProxy prx = GetWebProxy();
                if (prx != null) request.Proxy = prx;
            }
            catch (Exception) { Debug.Assert(false); return false; }

            request.CookieContainer = new CookieContainer();

            request.Timeout = 10000;  // milliseconds

            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            return true;
        }

        public static bool ConfigureWebClient(WebClient wc)
        {
            try
            {
                IWebProxy prx = GetWebProxy();
                if (prx != null) wc.Proxy = prx;
            }
            catch (Exception) { Debug.Assert(false); return false; }

            return true;
        }

        // Same logic as "private static KeePassLib.Serialization.IOConnection.GetWebProxy()"
        public static IWebProxy GetWebProxy()
        {
            IWebProxy prx = null;

            KeePass.App.Configuration.AceIntegration proxyConfig = Program.Config.Integration;
            switch (proxyConfig.ProxyType)
            {
                case ProxyServerType.None:
                    prx = null;
                    break;
                case ProxyServerType.Manual:
                    if (proxyConfig.ProxyAddress.Length != 0)
                    {
                        if (proxyConfig.ProxyPort.Length > 0)
                            prx = new WebProxy(proxyConfig.ProxyAddress, int.Parse(proxyConfig.ProxyPort));
                        else
                            prx = new WebProxy(proxyConfig.ProxyAddress);
                    }
                    else
                    {
                        // First try default (from config), then system
                        prx = WebRequest.DefaultWebProxy;
                        if (prx == null) prx = WebRequest.GetSystemWebProxy();
                    }
                    break;
                case ProxyServerType.System:
                    // First try system, then default (from config)
                    prx = WebRequest.GetSystemWebProxy();
                    if (prx == null) prx = WebRequest.DefaultWebProxy;
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
            if (prx == null) return null;

            // Authentication
            ProxyAuthType pat = proxyConfig.ProxyAuthType;
            if (pat == ProxyAuthType.Auto)
            {
                if ((proxyConfig.ProxyUserName.Length > 0) || (proxyConfig.ProxyPassword.Length > 0))
                    pat = ProxyAuthType.Manual;
                else
                    pat = ProxyAuthType.Default;
            }

            switch (pat)
            {
                case ProxyAuthType.None:
                    prx.Credentials = null;
                    break;
                case ProxyAuthType.Default:
                    prx.Credentials = CredentialCache.DefaultCredentials;
                    break;
                case ProxyAuthType.Manual:
                    if ((proxyConfig.ProxyUserName.Length > 0) || (proxyConfig.ProxyPassword.Length > 0))
                        prx.Credentials = new NetworkCredential(
                            proxyConfig.ProxyUserName, proxyConfig.ProxyPassword);
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            return prx;
        }

        // Same logic as "private static KeePassLib.Serialization.IOConnection.AcceptCertificate()"
        // Allow self-signed certificates, expired certificates, etc.
        private static bool AcceptCertificate(object sender,
            X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        // Same logic as "private static KeePassLib.Serialization.IOConnection.PrepareWebAccess()"
        public static void PrepareWebAccess()
        {
            try
            {
                bool sslCertsAcceptInvalid = Program.Config.Security.SslCertsAcceptInvalid;
                if (sslCertsAcceptInvalid)
                    ServicePointManager.ServerCertificateValidationCallback =
                        FaviconConnection.AcceptCertificate;
                else
                    ServicePointManager.ServerCertificateValidationCallback = null;
            }
            catch (Exception) { Debug.Assert(false); }

            try
            {
                SecurityProtocolType spt = (SecurityProtocolType.Ssl3 |
                    SecurityProtocolType.Tls);

                // The flags Tls11 and Tls12 in SecurityProtocolType have been
                // introduced in .NET 4.5 and must not be set when running under
                // older .NET versions (otherwise an exception is thrown)
                Type tSpt = typeof(SecurityProtocolType);
                string[] vSpt = Enum.GetNames(tSpt);
                foreach (string strSpt in vSpt)
                {
                    if (strSpt.Equals("Tls11", StrUtil.CaseIgnoreCmp))
                        spt |= (SecurityProtocolType)Enum.Parse(tSpt, "Tls11", true);
                    else if (strSpt.Equals("Tls12", StrUtil.CaseIgnoreCmp))
                        spt |= (SecurityProtocolType)Enum.Parse(tSpt, "Tls12", true);
                }

                ServicePointManager.SecurityProtocol = spt;
            }
            catch (Exception) { Debug.Assert(false); }
        }

        public static Uri GetMetaRefreshLink(Uri uri, HtmlDocument hdoc)
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
                catch (Exception) { /* Continue loop and try next one */ }
            }

            if (String.IsNullOrEmpty(redirect))
            {
                return null;
            }

            return new Uri(uri, redirect);
        }

        public static HttpWebRequest CreateWebRequest(Uri uri)
        {
            PrepareWebAccess();

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);

            ConfigureWebRequest(req);
            return req;
        }

        public static FaviconWebClient CreateWebClient()
        {
            PrepareWebAccess();

            FaviconWebClient wc = new FaviconWebClient();

            ConfigureWebClient(wc);
            return wc;
        }

        public static HtmlWeb CreateHtmlWebClient()
        {
            PrepareWebAccess();

            HtmlWeb hw = new HtmlWeb();

            hw.PreRequest += ConfigureWebRequest;
            return hw;
        }

        public static HttpWebResponse GetResponse(Uri uri)
        {
            return (HttpWebResponse)CreateWebRequest(uri).GetResponse();
        }

        public static Stream OpenRead(Uri uri)
        {
            if (StrUtil.IsDataUri(uri.AbsoluteUri))
            {
                byte[] pbData = StrUtil.DataUriToData(uri.AbsoluteUri);
                if (pbData != null) return new MemoryStream(pbData, false);
            }

            return CreateWebClient().OpenRead(uri);
        }

        public static HtmlDocument GetHtmlDocument(Uri uri)
        {
            return CreateHtmlWebClient().Load(uri);
        }

        public static HtmlDocument GetHtmlDocumentFollowMeta(ref Uri uri)
        {
            HtmlWeb hw = CreateHtmlWebClient();
            HtmlDocument hdoc = null;

            uint counter = 0;
            Uri nextUri = uri;
            do
            {
                // Load and follow HTTP redirects
                hdoc = hw.Load(uri);
                uri = hw.ResponseUri;

                // Follow HTML meta refresh
                nextUri = GetMetaRefreshLink(uri, hdoc);
            } while (nextUri != null && counter++ < 8);  // limit to 8 meta redirects

            return hdoc;
        }
    }
}
