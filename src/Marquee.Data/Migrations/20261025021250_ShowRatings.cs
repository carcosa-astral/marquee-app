using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marquee.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShowRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "UserRating",
                table: "Titles",
                type: "REAL",
                nullable: true);

            // Episodes are no longer rated. Each show gets the average of its episode ratings
            // (to the nearest half star), and the episode ratings are cleared.
            migrationBuilder.Sql("""
                UPDATE Titles SET UserRating = (
                    SELECT MIN(5.0, MAX(0.5, ROUND(AVG(w.Rating) * 2) / 2.0))
                    FROM Watches w
                    WHERE w.TitleId = Titles.Id AND w.EpisodeId IS NOT NULL AND w.Rating IS NOT NULL)
                WHERE EXISTS (
                    SELECT 1 FROM Watches w
                    WHERE w.TitleId = Titles.Id AND w.EpisodeId IS NOT NULL AND w.Rating IS NOT NULL);
                UPDATE Watches SET Rating = NULL WHERE EpisodeId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserRating",
                table: "Titles");
        }
    }
}
