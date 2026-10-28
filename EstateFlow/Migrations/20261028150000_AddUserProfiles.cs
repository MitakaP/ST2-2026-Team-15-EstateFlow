using EstateFlow.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace EstateFlow.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028150000_AddUserProfiles")]
public partial class AddUserProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserProfiles",
            columns: table => new
            {
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                Nickname = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                FirstName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                MiddleName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                LastName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                Bio = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                ProfileImagePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                table.ForeignKey("FK_UserProfiles_AspNetUsers_UserId", x => x.UserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_UserProfiles_Nickname", "UserProfiles", "Nickname", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("UserProfiles");
}
