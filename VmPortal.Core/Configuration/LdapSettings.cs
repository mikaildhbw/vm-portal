namespace VmPortal.Core.Configuration;

public class LdapSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = string.Empty;
}
