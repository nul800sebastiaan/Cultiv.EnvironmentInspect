using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cultiv.EnvironmentInspect.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CultivEnvironmentInspectUserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Sqlite:Autoincrement", true),
                    UserKey = table.Column<string>(nullable: false),
                    SettingKey = table.Column<string>(maxLength: 500, nullable: false),
                    IsStarred = table.Column<bool>(nullable: false),
                    StringValue = table.Column<string>(maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(nullable: false),
                    ModifiedDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CultivEnvironmentInspectUserPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CultivEnvironmentInspectUserPreferences_UserKey_SettingKey",
                table: "CultivEnvironmentInspectUserPreferences",
                columns: new[] { "UserKey", "SettingKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CultivEnvironmentInspectUserPreferences");
        }
    }
}
