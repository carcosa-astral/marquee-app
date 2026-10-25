using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marquee.Data.Migrations
{
    /// <inheritdoc />
    public partial class SourceScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AniListScore",
                table: "Titles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TmdbScore",
                table: "Titles",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AniListScore",
                table: "Titles");

            migrationBuilder.DropColumn(
                name: "TmdbScore",
                table: "Titles");
        }
    }
}
