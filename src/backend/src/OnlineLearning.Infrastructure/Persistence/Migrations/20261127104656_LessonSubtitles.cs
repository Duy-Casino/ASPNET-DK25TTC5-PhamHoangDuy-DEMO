using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LessonSubtitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubtitlePublicId",
                table: "Lessons",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubtitlePublicId",
                table: "Lessons");
        }
    }
}
