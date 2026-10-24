using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marquee.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbTypeAndPersonIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Titles_TmdbId",
                table: "Titles");

            migrationBuilder.AddColumn<string>(
                name: "TmdbType",
                table: "Titles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Titles_TmdbType_TmdbId",
                table: "Titles",
                columns: new[] { "TmdbType", "TmdbId" });

            migrationBuilder.CreateIndex(
                name: "IX_People_AniListId",
                table: "People",
                column: "AniListId");

            migrationBuilder.CreateIndex(
                name: "IX_People_Name",
                table: "People",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_People_TmdbId",
                table: "People",
                column: "TmdbId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Titles_TmdbType_TmdbId",
                table: "Titles");

            migrationBuilder.DropIndex(
                name: "IX_People_AniListId",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_Name",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_TmdbId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "TmdbType",
                table: "Titles");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_TmdbId",
                table: "Titles",
                column: "TmdbId");
        }
    }
}
