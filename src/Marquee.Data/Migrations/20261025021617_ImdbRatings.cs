using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marquee.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImdbRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ImdbRating",
                table: "Titles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImdbVotes",
                table: "Titles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbRating",
                table: "Titles");

            migrationBuilder.DropColumn(
                name: "ImdbVotes",
                table: "Titles");
        }
    }
}
