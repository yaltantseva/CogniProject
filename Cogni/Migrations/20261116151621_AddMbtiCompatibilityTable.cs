using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cogni.Migrations
{
    /// <inheritdoc />
    public partial class AddMbtiCompatibilityTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mbti_compatibility",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_mbti_type_id = table.Column<int>(type: "integer", nullable: false),
                    target_mbti_type_id = table.Column<int>(type: "integer", nullable: false),
                    compatibility_percentage = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mbti_compatibility_pkey", x => x.id);
                    table.ForeignKey(
                        name: "mbti_compatibility_source_fkey",
                        column: x => x.source_mbti_type_id,
                        principalTable: "mbti_type",
                        principalColumn: "id_mbti_type",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "mbti_compatibility_target_fkey",
                        column: x => x.target_mbti_type_id,
                        principalTable: "mbti_type",
                        principalColumn: "id_mbti_type",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mbti_compatibility_target_mbti_type_id",
                table: "mbti_compatibility",
                column: "target_mbti_type_id");

            migrationBuilder.CreateIndex(
                name: "mbti_compatibility_source_target_key",
                table: "mbti_compatibility",
                columns: new[] { "source_mbti_type_id", "target_mbti_type_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mbti_compatibility");
        }
    }
}
