using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartCompare.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItemCreatedByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_CreatedByUserId",
                table: "Items",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_AspNetUsers_CreatedByUserId",
                table: "Items",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_AspNetUsers_CreatedByUserId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_CreatedByUserId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Items");
        }
    }
}
