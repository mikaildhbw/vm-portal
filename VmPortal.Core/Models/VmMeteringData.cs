namespace VmPortal.Core.Models;

/// <summary>
/// Ressourcenverbrauchsdaten einer VM (Hyper-V: <c>Measure-VM</c>, setzt aktiviertes
/// Resource Metering via <c>Enable-VMResourceMetering</c> voraus).
/// </summary>
public record VmMeteringData(string AvgCpu, string AvgRam, string TotalDisk);
