using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoBotApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFeedbackToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrectIntent",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UserFeedback",
                table: "ChatMessages",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectIntent",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "UserFeedback",
                table: "ChatMessages");
        }
    }
}
