using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taxi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageKey",
                table: "Cars",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageKey",
                table: "Cars");
        }
    }
}
