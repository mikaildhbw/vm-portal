using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VmPortal.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedTestUserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "UserGroups",
                columns: new[] { "Id", "Name" },
                values: new object[] { 3, "ESXUserIT" });

            migrationBuilder.InsertData(
                table: "VirtualMachineGroups",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "Testumgebung-HVP" });

            migrationBuilder.InsertData(
                table: "GroupPermissions",
                columns: new[] { "Id", "RoleId", "UserGroupId", "VmGroupId" },
                values: new object[] { 1, 3, 3, 1 });

            migrationBuilder.InsertData(
                table: "VirtualMachines",
                columns: new[] { "Id", "GroupId", "Name", "ServerId" },
                values: new object[,]
                {
                    { 1, 1, "HVP_1", 3 },
                    { 2, 1, "HVP_2", 3 },
                    { 3, 1, "HVP_3", 3 },
                    { 4, 1, "HVP_4", 3 },
                    { 5, 1, "HVP_5", 3 },
                    { 6, 1, "HVP_6", 3 },
                    { 7, 1, "HVP_7", 3 },
                    { 8, 1, "HVP_8", 3 },
                    { 9, 1, "HVP_9", 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GroupPermissions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "UserGroups",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "VirtualMachineGroups",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
