using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmPortal.Core.Data.Migrations
{
    /// <summary>
    /// Testdaten für Kapitel-7.2-Sicherheitstestfall TF5 (Bachelorarbeit): Zugriff auf eine VM,
    /// die existiert, aber dem Testbenutzer ("ugur") NICHT zugewiesen ist -> erwartet 403.
    /// Legt bewusst NUR die VM-Gruppe und die VM-Mitgliedschaft an, KEINE GroupPermission -
    /// die VM ist damit secure-by-default für alle AD-Gruppen unerreichbar, siehe
    /// DbAuthorizationService.GetAllowedActionsAsync (VM ohne GroupPermission -> leere
    /// Aktionsmenge -> 403 im VmController). Analog zu SeedTestUserPermissions: reine
    /// InsertData/DeleteData-Migration statt HasData, da nachträglich zum permanenten Seed
    /// hinzugefügt (siehe AuthorizationSeedData).
    /// </summary>
    public partial class SeedTf5ForeignVmTestCase : Migration
    {
        // VirtualMachineGroups.Id: 1 ist bereits durch SeedTestUserPermissions (Testumgebung-HVP) belegt.
        private const int Tf5VmGroupId = 2;
        // VirtualMachines.Id: 1-9 sind bereits durch SeedTestUserPermissions (HVP_1-HVP_9) belegt.
        private const int Tf5VmId = 10;
        private const int Hyperv3ServerId = 2; // MHM-HYPERV3, siehe AuthorizationSeedData.VirtualServers
        // Get-VM -ComputerName mhm-hyperv3 -Name HVP_15 | Select VMId - vom Nutzer bestätigt, keine Platzhalter-GUID.
        private const string Tf5VmGuid = "71861831-98b3-4877-a3c6-9e5e508e516e";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "VirtualMachineGroups",
                columns: new[] { "Id", "Name" },
                values: new object[] { Tf5VmGroupId, "TF5-Fremd-VM-Test" });

            migrationBuilder.InsertData(
                table: "VirtualMachines",
                columns: new[] { "Id", "GroupId", "Name", "ServerId", "VmGuid" },
                values: new object[] { Tf5VmId, Tf5VmGroupId, "HVP_15", Hyperv3ServerId, Tf5VmGuid });

            // Bewusst KEINE GroupPermission für Tf5VmGroupId: das ist der ganze Zweck von TF5 -
            // die VM ist bekannt, aber für keine AD-Gruppe autorisiert.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: Tf5VmId);

            migrationBuilder.DeleteData(
                table: "VirtualMachineGroups",
                keyColumn: "Id",
                keyValue: Tf5VmGroupId);
        }
    }
}
