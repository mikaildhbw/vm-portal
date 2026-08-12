namespace VmPortal.Core.Data.Entities;

public class VirtualMachineGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<VirtualMachineRecord> VirtualMachines { get; set; } = new();
    public List<GroupPermission> GroupPermissions { get; set; } = new();
}
