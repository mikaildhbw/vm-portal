using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmPortal.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixVirtualServersHostCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "VirtualServers",
                keyColumn: "Id",
                keyValue: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "VirtualServers",
                columns: new[] { "Id", "Address", "Name", "Platform" },
                values: new object[] { 4, "MHM-VCLUSTER1.archiv.mhm.siemens.com", "MHM-VCLUSTER1", "HyperV" });
        }
    }
}
