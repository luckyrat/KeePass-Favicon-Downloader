using System;
using System.Diagnostics;
using System.IO;
using System.Net;

using System.Net.Cache;
using System.Net.Security;

using System.Security.Cryptography.X509Certificates;

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
        internal static void ConfigureWebRequest(HttpWebRequest request)
        {
            try
            {
                IWebProxy prx = GetWebProxy();
                if (prx != null) request.Proxy = prx;
            }
            catch (Exception) { Debug.Assert(false); }

            request.Timeout = 5000;  // milliseconds

            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
        }

        internal static void ConfigureWebClient(WebClient wc)
        {
            try
            {
                IWebProxy prx = GetWebProxy();
                if (prx != null) wc.Proxy = prx;
            }
            catch (Exception) { Debug.Assert(false); }
        }

        public static IWebProxy GetWebProxy()
        {
            IWebProxy prx = null;

            KeePass.App.Configuration.AceIntegration proxyConfig = Program.Config.Integration;
            switch (proxyConfig.ProxyType)
            {
                case ProxyServerType.None:
                    prx = null; // Use null proxy
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

            // Authentication
            if (prx == null) return prx;

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

        // Allow self-signed certificates, expired certificates, etc.
        private static bool AcceptCertificate(object sender,
            X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

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

        public static HttpWebResponse GetResponse(Uri uri)
        {
            return (HttpWebResponse)CreateWebRequest(uri).GetResponse();
        }

        public static Stream OpenRead(Uri uri)
        {
            return CreateWebClient().OpenRead(uri);
        }
    }
}
