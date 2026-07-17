using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "ServiceTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryDevicePhotoPath",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryIdentityDocumentPhotoPath",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRecipientFullName",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "DeliveryDevicePhotoPath",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "DeliveryIdentityDocumentPhotoPath",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "DeliveryRecipientFullName",
                table: "ServiceTickets");
        }
    }
}
