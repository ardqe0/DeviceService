using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAndIdentityBackPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryDeviceBackPhotoPath",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryIdentityDocumentBackPhotoPath",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryDeviceBackPhotoPath",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "DeliveryIdentityDocumentBackPhotoPath",
                table: "ServiceTickets");
        }
    }
}
