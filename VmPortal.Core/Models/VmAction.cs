namespace VmPortal.Core.Models;

/// <summary>
/// Katalog aller autorisierbaren VM-Aktionen. Die Zuordnung zur jeweils
/// mindestens benötigten Rolle erfolgt in <c>RolePermissions</c>.
/// </summary>
public enum VmAction
{
    ViewStatus,
    ViewDetails,
    ViewMetering,
    Start,
    Stop,
    Pause,
    Resume,
    SaveState,
    Reset,
    SnapshotCreate,
    SnapshotApply,
    ConsoleConnect,
    SnapshotDelete,
    ResizeRam,
    ResizeCpu,
    AttachNetworkAdapter,
    VhdResize,
    VhdCompact,
    Export,
    Import,
    Clone,
    LiveMigrate
}
