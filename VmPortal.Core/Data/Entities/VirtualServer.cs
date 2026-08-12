namespace VmPortal.Core.Data.Entities;

public class VirtualServer
{
    public int Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
