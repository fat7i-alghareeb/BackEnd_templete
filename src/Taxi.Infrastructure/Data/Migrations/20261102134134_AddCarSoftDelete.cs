using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taxi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Cars",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Cars",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Cars",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Cars");
        }
    }
}
