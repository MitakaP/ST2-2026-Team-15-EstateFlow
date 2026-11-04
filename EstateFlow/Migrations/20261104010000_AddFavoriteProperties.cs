using EstateFlow.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace EstateFlow.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104010000_AddFavoriteProperties")]
public partial class AddFavoriteProperties : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FavoriteProperties",
            columns: table => new
            {
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FavoriteProperties", x => new { x.UserId, x.PropertyId });
                table.ForeignKey("FK_FavoriteProperties_AspNetUsers_UserId", x => x.UserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("FavoriteProperties");
}
