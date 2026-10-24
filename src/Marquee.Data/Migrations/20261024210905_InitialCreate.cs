using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marquee.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DeathDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", nullable: true),
                    PhotoPath = table.Column<string>(type: "TEXT", nullable: true),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    AniListId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Theaters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    Chain = table.Column<string>(type: "TEXT", nullable: true),
                    Formats = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Theaters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Titles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Overview = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: true),
                    BackdropPath = table.Column<string>(type: "TEXT", nullable: true),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    AniListId = table.Column<int>(type: "INTEGER", nullable: true),
                    MalId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Titles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Credits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    PersonId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Character = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Credits_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Credits_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldOverrides",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldOverrides_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldSources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldSources_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnIce",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    StoppedAtSeason = table.Column<int>(type: "INTEGER", nullable: true),
                    StoppedAtEpisode = table.Column<int>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    IcedOn = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnIce", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnIce_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TitleGenres",
                columns: table => new
                {
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    GenreId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitleGenres", x => new { x.TitleId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_TitleGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TitleGenres_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watchlist",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    AddedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpectedRelease = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watchlist_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonId = table.Column<string>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    AirDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RuntimeMinutes = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TitleId = table.Column<string>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<string>(type: "TEXT", nullable: true),
                    WatchedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    Rating = table.Column<double>(type: "REAL", nullable: true),
                    IsRewatch = table.Column<bool>(type: "INTEGER", nullable: false),
                    Where = table.Column<string>(type: "TEXT", nullable: false),
                    TheaterId = table.Column<string>(type: "TEXT", nullable: true),
                    Screen = table.Column<string>(type: "TEXT", nullable: true),
                    Seat = table.Column<string>(type: "TEXT", nullable: true),
                    Format = table.Column<string>(type: "TEXT", nullable: true),
                    Service = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watches_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Watches_Theaters_TheaterId",
                        column: x => x.TheaterId,
                        principalTable: "Theaters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Watches_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchCompanions",
                columns: table => new
                {
                    WatchId = table.Column<string>(type: "TEXT", nullable: false),
                    CompanionId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchCompanions", x => new { x.WatchId, x.CompanionId });
                    table.ForeignKey(
                        name: "FK_WatchCompanions_Companions_CompanionId",
                        column: x => x.CompanionId,
                        principalTable: "Companions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchCompanions_Watches_WatchId",
                        column: x => x.WatchId,
                        principalTable: "Watches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Credits_PersonId",
                table: "Credits",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Credits_TitleId",
                table: "Credits",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeasonId_Number",
                table: "Episodes",
                columns: new[] { "SeasonId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldOverrides_TitleId_FieldName",
                table: "FieldOverrides",
                columns: new[] { "TitleId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldSources_TitleId_FieldName",
                table: "FieldSources",
                columns: new[] { "TitleId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnIce_TitleId",
                table: "OnIce",
                column: "TitleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_TitleId_Number",
                table: "Seasons",
                columns: new[] { "TitleId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_TitleGenres_GenreId",
                table: "TitleGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_AniListId",
                table: "Titles",
                column: "AniListId");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_MalId",
                table: "Titles",
                column: "MalId");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_Name",
                table: "Titles",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_TmdbId",
                table: "Titles",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchCompanions_CompanionId",
                table: "WatchCompanions",
                column: "CompanionId");

            migrationBuilder.CreateIndex(
                name: "IX_Watches_EpisodeId",
                table: "Watches",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Watches_TheaterId",
                table: "Watches",
                column: "TheaterId");

            migrationBuilder.CreateIndex(
                name: "IX_Watches_TitleId",
                table: "Watches",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Watches_WatchedOn",
                table: "Watches",
                column: "WatchedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Watchlist_TitleId",
                table: "Watchlist",
                column: "TitleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Credits");

            migrationBuilder.DropTable(
                name: "FieldOverrides");

            migrationBuilder.DropTable(
                name: "FieldSources");

            migrationBuilder.DropTable(
                name: "OnIce");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "TitleGenres");

            migrationBuilder.DropTable(
                name: "WatchCompanions");

            migrationBuilder.DropTable(
                name: "Watchlist");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Companions");

            migrationBuilder.DropTable(
                name: "Watches");

            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropTable(
                name: "Theaters");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Titles");
        }
    }
}
