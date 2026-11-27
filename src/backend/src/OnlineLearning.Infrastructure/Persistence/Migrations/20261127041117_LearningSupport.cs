using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningActionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningActionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningSupportRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    Response = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSupportRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSupportRequests_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LearningSupportRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuizAttemptGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtraAttempts = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GrantedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAttemptGrants", x => x.Id);
                    table.CheckConstraint("CK_QuizAttemptGrants_Extra", "[ExtraAttempts] BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_QuizAttemptGrants_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizAttemptGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningActionLogs_CreatedAt",
                table: "LearningActionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LearningActionLogs_LearnerId_CourseId_CreatedAt",
                table: "LearningActionLogs",
                columns: new[] { "LearnerId", "CourseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSupportRequests_CourseId",
                table: "LearningSupportRequests",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSupportRequests_Status_CreatedAt",
                table: "LearningSupportRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSupportRequests_UserId_CourseId",
                table: "LearningSupportRequests",
                columns: new[] { "UserId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttemptGrants_QuizId_UserId",
                table: "QuizAttemptGrants",
                columns: new[] { "QuizId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttemptGrants_UserId",
                table: "QuizAttemptGrants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningActionLogs");

            migrationBuilder.DropTable(
                name: "LearningSupportRequests");

            migrationBuilder.DropTable(
                name: "QuizAttemptGrants");
        }
    }
}
