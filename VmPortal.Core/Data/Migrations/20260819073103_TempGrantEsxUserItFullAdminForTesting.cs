using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmPortal.Core.Data.Migrations
{
    /// <summary>
    /// ⚠️ TEMPORÄR — NUR FÜR DIE TESTPHASE DER NEUEN ADMIN-ENDPUNKTE (2026-08-19). ⚠️
    ///
    /// Gibt der UserGroup "ESXUserIT" zusätzlich zur bestehenden PowerUser-Berechtigung aus
    /// <see cref="SeedTestUserPermissions"/> eine FullAdmin-GroupPermission auf dieselbe
    /// VM-Gruppe "Testumgebung-HVP" - der Verfasser hat keinen echten "ESX Admins"-Account
    /// und braucht FullAdmin, um /api/admin/discover-vms, /api/admin/vm-groups/* und
    /// /api/admin/ad-groups mit seinem regulären ESXUserIT-Konto zu testen (diese Endpunkte
    /// prüfen ausschließlich Bootstrap-FullAdmin, siehe AdminControllerBase - eine VM-Gruppen-
    /// Rolle wie PowerUser reicht dafür nicht).
    ///
    /// Bewusst NICHT über AuthorizationSeedData/OnModelCreating (HasData) eingebunden,
    /// sondern als eigenständige Insert-/Delete-Data-Migration: der permanente
    /// Seed-Daten-Quellcode (die "offizielle" Beschreibung des Ausgangszustands, auf die sich
    /// auch CLAUDE.md/README.md beziehen) bleibt dadurch unberührt von diesem befristeten
    /// Testzustand.
    ///
    /// RÜCKBAU nach Abschluss der Admin-Panel-Tests (entfernt NUR diese FullAdmin-Zeile,
    /// die PowerUser-Berechtigung aus SeedTestUserPermissions bleibt unangetastet):
    ///   dotnet ef database update AddVmGuidToVirtualMachines --project VmPortal.Core --startup-project VmPortal.Api
    /// </summary>
    public partial class TempGrantEsxUserItFullAdminForTesting : Migration
    {
        // GroupPermissions.Id: 1 ist bereits durch SeedTestUserPermissions (PowerUser) belegt.
        private const int TempFullAdminGroupPermissionId = 2;
        private const int EsxUserItUserGroupId = 3;         // siehe SeedTestUserPermissions
        private const int TestumgebungHvpVmGroupId = 1;      // siehe SeedTestUserPermissions
        private const int FullAdminRoleId = 5;               // AuthorizationSeedData.RoleId(VmRole.FullAdmin) = 4 + 1

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "GroupPermissions",
                columns: new[] { "Id", "RoleId", "UserGroupId", "VmGroupId" },
                values: new object[] { TempFullAdminGroupPermissionId, FullAdminRoleId, EsxUserItUserGroupId, TestumgebungHvpVmGroupId });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GroupPermissions",
                keyColumn: "Id",
                keyValue: TempFullAdminGroupPermissionId);
        }
    }
}
