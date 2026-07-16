using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingVerificationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackingVerificationAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RemoteAddress = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingVerificationAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingVerificationAttempts_Token_RemoteAddress",
                table: "TrackingVerificationAttempts",
                columns: new[] { "Token", "RemoteAddress" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackingVerificationAttempts");
        }
    }
}
