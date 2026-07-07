namespace VmPortal.Core.Models;

public class VirtualMachine
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public VmStatus Status { get; set; }
    public string AssignedUserId { get; set; } = string.Empty;
}

public enum VmStatus
{
    Running,
    Stopped,
    Paused,
    Unknown
}
