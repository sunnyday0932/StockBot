using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProbeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntityMatchCount",
                table: "SourceDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProbedAt",
                table: "SourceDocuments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntityMatchCount",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "ProbedAt",
                table: "SourceDocuments");
        }
    }
}
