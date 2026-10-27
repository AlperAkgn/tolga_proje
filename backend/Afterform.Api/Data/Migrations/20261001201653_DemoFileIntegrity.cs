using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoFileIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileSha256",
                table: "Demos",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RightsConfirmedAtUtc",
                table: "Demos",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSha256",
                table: "Demos");

            migrationBuilder.DropColumn(
                name: "RightsConfirmedAtUtc",
                table: "Demos");
        }
    }
}
