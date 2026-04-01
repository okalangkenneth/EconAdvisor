using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EconAdvisor.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "indicator_series",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    series_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicator_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "indicator_observations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    series_id = table.Column<int>(type: "integer", nullable: false),
                    observation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_indicator_observations", x => x.id);
                    table.ForeignKey(
                        name: "FK_indicator_observations_indicator_series_series_id",
                        column: x => x.series_id,
                        principalTable: "indicator_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_indicator_observations_fetched_at",
                table: "indicator_observations",
                column: "fetched_at");

            migrationBuilder.CreateIndex(
                name: "IX_indicator_observations_series_id_observation_date",
                table: "indicator_observations",
                columns: new[] { "series_id", "observation_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_indicator_series_country_code_series_key",
                table: "indicator_series",
                columns: new[] { "country_code", "series_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "indicator_observations");

            migrationBuilder.DropTable(
                name: "indicator_series");
        }
    }
}
