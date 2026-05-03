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
                    UserKey = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SettingKey = table.Column<string>(type: "nvarchar(250)", nullable: false),
                    IsStarred = table.Column<bool>(type: "bit", nullable: false),
                    StringValue = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
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
