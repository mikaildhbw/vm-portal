using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmPortal.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVmGuidToVirtualMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VmGuid",
                table: "VirtualMachines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 1,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 2,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 3,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 4,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 5,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 6,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 7,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 8,
                column: "VmGuid",
                value: null);

            migrationBuilder.UpdateData(
                table: "VirtualMachines",
                keyColumn: "Id",
                keyValue: 9,
                column: "VmGuid",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VmGuid",
                table: "VirtualMachines");
        }
    }
}
