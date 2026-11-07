using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetShopApp.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMoreInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MoreInformation",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoreInformation",
                table: "Products");
        }
    }
}
