using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace USPSimGame.Migrations
{
    /// <inheritdoc />
    public partial class Primary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CenterLatLong = table.Column<string>(type: "text", nullable: false),
                    Zoom = table.Column<int>(type: "integer", nullable: false),
                    StartYear = table.Column<int>(type: "integer", nullable: false),
                    CurrentMonth = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapLayerDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    LayerType = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsEnabledByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TranslatorTags = table.Column<string>(type: "text", nullable: true),
                    SimulatorTags = table.Column<string>(type: "text", nullable: true),
                    DefaultStyleConfigJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapLayerDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActive = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameSessionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameSessionMapLayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameSessionId = table.Column<int>(type: "integer", nullable: false),
                    MapLayerDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CachedDataContent = table.Column<string>(type: "text", nullable: true),
                    TranslatorTags = table.Column<string>(type: "text", nullable: true),
                    SimulatorTags = table.Column<string>(type: "text", nullable: true),
                    LastFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessionMapLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessionMapLayers_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameSessionMapLayers_MapLayerDefinitions_MapLayerDefinition~",
                        column: x => x.MapLayerDefinitionId,
                        principalTable: "MapLayerDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MapLayerDefinitions",
                columns: new[] { "Id", "Category", "DefaultStyleConfigJson", "Description", "IsEnabledByDefault", "Key", "LayerType", "Name", "SimulatorTags", "TranslatorTags" },
                values: new object[,]
                {
                    { 1, "Buildings", null, "Extruded 3D building footprints derived from 3D BAG lidar elevation data (LoD 1.3).", true, "pdok-3dbag-buildings", "VectorGeoJson", "3D BAG Buildings (2.5D Extruded)", null, null },
                    { 2, "Infrastructure", null, "Electrical grid infrastructure featuring low-, medium-, and high-voltage cables and transformer stations for Liander territory.", false, "liander-open-data-elektra", "VectorGeoJson", "Liander Electricity Network (Cables & Stations)", null, null },
                    { 3, "Infrastructure", null, "Complete low-, medium-, and high-voltage electricity cables and transformer stations for Stedin service territory.", false, "stedin-open-data-elektra", "VectorGeoJson", "Stedin Regional Electricity Grid", null, null },
                    { 4, "Infrastructure", null, "Municipal urban water, sewage pipes, inspection manholes, and pumping stations from Stichting Rioned GWSW.", false, "pdok-gwsw-sewage", "VectorGeoJson", "Urban Sewage & Drainage Network (PDOK GWSW)", null, null },
                    { 5, "Environment", null, "Official municipal boundaries and administrative jurisdiction borders derived from Kadaster BRK.", false, "pdok-brk-bestuurlijkegebieden", "VectorGeoJson", "Municipal Boundaries (BRK Bestuurlijke Gebieden)", null, null },
                    { 6, "Environment", null, "Official cadastral land parcel boundaries, plot sizes, section codes, and parcel numbers from Kadaster BRK.", false, "pdok-brk-kadastralekaart", "VectorGeoJson", "Cadastral Parcels (BRK Kadastrale Kaart WFS)", null, null }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "PasswordHash", "Username" },
                values: new object[] { 1, "harald.warmelink@hu.nl", "", "Admin" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionMapLayers_GameSessionId",
                table: "GameSessionMapLayers",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionMapLayers_MapLayerDefinitionId",
                table: "GameSessionMapLayers",
                column: "MapLayerDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_GameSessionId",
                table: "Teams",
                column: "GameSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSessionMapLayers");

            migrationBuilder.DropTable(
                name: "PlayerSessions");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "MapLayerDefinitions");

            migrationBuilder.DropTable(
                name: "GameSessions");
        }
    }
}
