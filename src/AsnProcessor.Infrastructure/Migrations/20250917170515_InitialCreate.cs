using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsnProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierIdentifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ChecksumSha256 = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoxLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoxId = table.Column<int>(type: "INTEGER", nullable: false),
                    PoNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Isbn = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoxLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoxLines_Boxes_BoxId",
                        column: x => x.BoxId,
                        principalTable: "Boxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_SupplierIdentifier_Identifier",
                table: "Boxes",
                columns: new[] { "SupplierIdentifier", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoxLines_BoxId",
                table: "BoxLines",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_ChecksumSha256",
                table: "ProcessedFiles",
                column: "ChecksumSha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoxLines");

            migrationBuilder.DropTable(
                name: "ProcessedFiles");

            migrationBuilder.DropTable(
                name: "Boxes");
        }
    }
}
