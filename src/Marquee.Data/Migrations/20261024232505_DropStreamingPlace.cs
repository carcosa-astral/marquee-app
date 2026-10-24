using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marquee.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropStreamingPlace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Streaming" is no longer a place. Those watches become Home; the service is kept.
            migrationBuilder.Sql("UPDATE Watches SET \"Where\" = 'home' WHERE \"Where\" = 'streaming';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Home watches used to be Streaming is not recorded.
        }
    }
}
