using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerServiceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceAccountId",
                table: "Customers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceAccountId",
                table: "Customers");
        }
    }
}
