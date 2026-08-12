using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VmPortal.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthorizationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualMachineGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMachineGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VMActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VMActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VmGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupPermissions_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupPermissions_VirtualMachineGroups_VmGroupId",
                        column: x => x.VmGroupId,
                        principalTable: "VirtualMachineGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VirtualMachines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMachines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualMachines_VirtualMachineGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "VirtualMachineGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VirtualMachines_VirtualServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "VirtualServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleActions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleActions", x => new { x.RoleId, x.ActionId });
                    table.ForeignKey(
                        name: "FK_RoleActions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleActions_VMActions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "VMActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "IsSystemRole", "Level", "Name" },
                values: new object[,]
                {
                    { 1, true, 0, "Viewer" },
                    { 2, true, 1, "Operator" },
                    { 3, true, 2, "PowerUser" },
                    { 4, true, 3, "Admin" },
                    { 5, true, 4, "FullAdmin" }
                });

            migrationBuilder.InsertData(
                table: "UserGroups",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "ESX Admins" },
                    { 2, "VM-Portal-Benutzer" }
                });

            migrationBuilder.InsertData(
                table: "VMActions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "ViewStatus" },
                    { 2, "ViewDetails" },
                    { 3, "ViewMetering" },
                    { 4, "Start" },
                    { 5, "Stop" },
                    { 6, "Pause" },
                    { 7, "Resume" },
                    { 8, "SaveState" },
                    { 9, "Reset" },
                    { 10, "SnapshotCreate" },
                    { 11, "SnapshotApply" },
                    { 12, "ConsoleConnect" },
                    { 13, "SnapshotDelete" },
                    { 14, "ResizeRam" },
                    { 15, "ResizeCpu" },
                    { 16, "AttachNetworkAdapter" },
                    { 17, "VhdResize" },
                    { 18, "VhdCompact" },
                    { 19, "Export" },
                    { 20, "Import" },
                    { 21, "Clone" },
                    { 22, "LiveMigrate" }
                });

            migrationBuilder.InsertData(
                table: "VirtualServers",
                columns: new[] { "Id", "Address", "Name", "Platform" },
                values: new object[,]
                {
                    { 1, "MHM-HYPERV1.archiv.mhm.siemens.com", "MHM-HYPERV1", "HyperV" },
                    { 2, "MHM-HYPERV3.archiv.mhm.siemens.com", "MHM-HYPERV3", "HyperV" },
                    { 3, "MHM-HYPERV4.archiv.mhm.siemens.com", "MHM-HYPERV4", "HyperV" },
                    { 4, "MHM-VCLUSTER1.archiv.mhm.siemens.com", "MHM-VCLUSTER1", "HyperV" }
                });

            migrationBuilder.InsertData(
                table: "RoleActions",
                columns: new[] { "ActionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 5, 2 },
                    { 6, 2 },
                    { 7, 2 },
                    { 8, 2 },
                    { 1, 3 },
                    { 2, 3 },
                    { 3, 3 },
                    { 4, 3 },
                    { 5, 3 },
                    { 6, 3 },
                    { 7, 3 },
                    { 8, 3 },
                    { 9, 3 },
                    { 10, 3 },
                    { 11, 3 },
                    { 12, 3 },
                    { 1, 4 },
                    { 2, 4 },
                    { 3, 4 },
                    { 4, 4 },
                    { 5, 4 },
                    { 6, 4 },
                    { 7, 4 },
                    { 8, 4 },
                    { 9, 4 },
                    { 10, 4 },
                    { 11, 4 },
                    { 12, 4 },
                    { 13, 4 },
                    { 14, 4 },
                    { 15, 4 },
                    { 16, 4 },
                    { 17, 4 },
                    { 18, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 4, 5 },
                    { 5, 5 },
                    { 6, 5 },
                    { 7, 5 },
                    { 8, 5 },
                    { 9, 5 },
                    { 10, 5 },
                    { 11, 5 },
                    { 12, 5 },
                    { 13, 5 },
                    { 14, 5 },
                    { 15, 5 },
                    { 16, 5 },
                    { 17, 5 },
                    { 18, 5 },
                    { 19, 5 },
                    { 20, 5 },
                    { 21, 5 },
                    { 22, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPermissions_RoleId",
                table: "GroupPermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPermissions_UserGroupId",
                table: "GroupPermissions",
                column: "UserGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPermissions_VmGroupId_UserGroupId_RoleId",
                table: "GroupPermissions",
                columns: new[] { "VmGroupId", "UserGroupId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleActions_ActionId",
                table: "RoleActions",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_Name",
                table: "UserGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualMachineGroups_Name",
                table: "VirtualMachineGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualMachines_GroupId",
                table: "VirtualMachines",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualMachines_ServerId",
                table: "VirtualMachines",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_VMActions_Name",
                table: "VMActions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupPermissions");

            migrationBuilder.DropTable(
                name: "RoleActions");

            migrationBuilder.DropTable(
                name: "VirtualMachines");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "VMActions");

            migrationBuilder.DropTable(
                name: "VirtualMachineGroups");

            migrationBuilder.DropTable(
                name: "VirtualServers");
        }
    }
}
