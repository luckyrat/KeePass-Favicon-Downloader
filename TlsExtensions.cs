using System.Security.Authentication;

namespace System.Net
{
    /// <summary>
    /// https://support.microsoft.com/en-gb/help/3154517/support-for-tls-system-default-versions-included-in-the-net-framework
    /// </summary>
    public static class SecurityProtocolTypeExtensions
    {
        public const SecurityProtocolType Tls12 = (SecurityProtocolType)SslProtocolsExtensions.Tls12;
        public const SecurityProtocolType Tls11 = (SecurityProtocolType)SslProtocolsExtensions.Tls11;
        public const SecurityProtocolType SystemDefault = (SecurityProtocolType)0;
    }
}

namespace System.Security.Authentication
{
    /// <summary>
    /// https://support.microsoft.com/en-gb/help/3154517/support-for-tls-system-default-versions-included-in-the-net-framework
    /// </summary>
    public static class SslProtocolsExtensions
    {
        public const SslProtocols Tls12 = (SslProtocols)0x00000C00;
        public const SslProtocols Tls11 = (SslProtocols)0x00000300;
    }
}
