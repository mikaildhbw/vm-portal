namespace VmPortal.Core.Configuration;

public class HyperVSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5986;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}
