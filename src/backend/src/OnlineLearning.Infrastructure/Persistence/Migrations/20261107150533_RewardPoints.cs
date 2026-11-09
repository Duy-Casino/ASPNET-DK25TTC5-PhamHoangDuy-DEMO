using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestrictedToUserId",
                table: "Coupons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    CreatorKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OpenCount = table.Column<int>(type: "int", nullable: false),
                    FirstOpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RewardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseShares_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CourseShares_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyCheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StreakCount = table.Column<int>(type: "int", nullable: false),
                    PointsAwarded = table.Column<int>(type: "int", nullable: false),
                    StreakBonusAwarded = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCheckIns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    VoucherValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CouponCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointRedemptions", x => x.Id);
                    table.CheckConstraint("CK_PointRedemptions_Points", "[Points] > 0");
                    table.ForeignKey(
                        name: "FK_PointRedemptions_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PointRedemptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    AmountUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Threshold = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointRules", x => x.Id);
                    table.CheckConstraint("CK_PointRules_Points", "[Points] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferralCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferredUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RewardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RewardOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_Users_ReferredUserId",
                        column: x => x.ReferredUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Referrals_Users_ReferrerId",
                        column: x => x.ReferrerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<int>(type: "int", nullable: false),
                    TotalEarned = table.Column<int>(type: "int", nullable: false),
                    TotalSpent = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPoints", x => x.Id);
                    table.CheckConstraint("CK_UserPoints_Balance", "[Balance] >= 0");
                    table.CheckConstraint("CK_UserPoints_Totals", "[TotalEarned] >= 0 AND [TotalSpent] >= 0");
                    table.ForeignKey(
                        name: "FK_UserPoints_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseShareOpens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseShareId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitorKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VisitorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseShareOpens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseShareOpens_CourseShares_CourseShareId",
                        column: x => x.CourseShareId,
                        principalTable: "CourseShares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_RestrictedToUserId",
                table: "Coupons",
                column: "RestrictedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseShareOpens_CourseShareId_VisitorKey",
                table: "CourseShareOpens",
                columns: new[] { "CourseShareId", "VisitorKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseShares_CourseId_CreatedAt",
                table: "CourseShares",
                columns: new[] { "CourseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseShares_Token",
                table: "CourseShares",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseShares_UserId_CreatedAt",
                table: "CourseShares",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckIns_UserId_CheckInDate",
                table: "DailyCheckIns",
                columns: new[] { "UserId", "CheckInDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointRedemptions_CouponId",
                table: "PointRedemptions",
                column: "CouponId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointRedemptions_UserId_CreatedAt",
                table: "PointRedemptions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointRules_Key",
                table: "PointRules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CreatedAt",
                table: "PointTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_RuleKey_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "RuleKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_UserId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_UserId_ReferenceKey",
                table: "PointTransactions",
                columns: new[] { "UserId", "ReferenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_Code",
                table: "ReferralCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_UserId",
                table: "ReferralCodes",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredUserId",
                table: "Referrals",
                column: "ReferredUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerId",
                table: "Referrals",
                column: "ReferrerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_Balance",
                table: "UserPoints",
                column: "Balance");

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_UserId",
                table: "UserPoints",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseShareOpens");

            migrationBuilder.DropTable(
                name: "DailyCheckIns");

            migrationBuilder.DropTable(
                name: "PointRedemptions");

            migrationBuilder.DropTable(
                name: "PointRules");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "ReferralCodes");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropTable(
                name: "UserPoints");

            migrationBuilder.DropTable(
                name: "CourseShares");

            migrationBuilder.DropIndex(
                name: "IX_Coupons_RestrictedToUserId",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RestrictedToUserId",
                table: "Coupons");
        }
    }
}
