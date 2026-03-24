using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace StockBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "DiscoveredConcepts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceDocumentId = table.Column<string>(type: "text", nullable: false),
                    AssociatedStockCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AppearanceCount = table.Column<int>(type: "integer", nullable: false),
                    FirstDiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsApprovedAndPromoted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentEmbedding",
                columns: table => new
                {
                    DocumentId = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    SentimentScore = table.Column<float>(type: "real", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEmbedding", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "SourceDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PttUpvoteCount = table.Column<int>(type: "integer", nullable: true),
                    PttDownvoteCount = table.Column<int>(type: "integer", nullable: true),
                    PttArrowCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceDocuments", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "TrackedEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    PrimaryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StockCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityAliases_TrackedEntities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "TrackedEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredConcepts_IsApprovedAndPromoted",
                table: "DiscoveredConcepts",
                column: "IsApprovedAndPromoted");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredConcepts_Keyword",
                table: "DiscoveredConcepts",
                column: "Keyword");

            migrationBuilder.CreateIndex(
                name: "IX_EntityAliases_EntityId",
                table: "EntityAliases",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityAliases_Keyword",
                table: "EntityAliases",
                column: "Keyword");

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocuments_PublishedAt",
                table: "SourceDocuments",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocuments_SourceType",
                table: "SourceDocuments",
                column: "SourceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveredConcepts");

            migrationBuilder.DropTable(
                name: "DocumentEmbedding");

            migrationBuilder.DropTable(
                name: "EntityAliases");

            migrationBuilder.DropTable(
                name: "SourceDocuments");

            migrationBuilder.DropTable(
                name: "TrackedEntities");
        }
    }
}
