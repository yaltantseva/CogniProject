using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cogni.Migrations
{
    /// <inheritdoc />
    public partial class ExpandArticleBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "article_body",
                table: "article",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "article_body",
                table: "article",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
