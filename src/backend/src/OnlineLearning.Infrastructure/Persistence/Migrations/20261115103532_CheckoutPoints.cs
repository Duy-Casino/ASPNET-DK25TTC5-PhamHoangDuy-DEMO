using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineLearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckoutPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Math",
                table: "Orders");

            migrationBuilder.AddColumn<decimal>(
                name: "PointsDiscountAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PointsUsed",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Math",
                table: "Orders",
                sql: "[TotalAmount] = [SubTotal] - [DiscountAmount] - [PointsDiscountAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Points",
                table: "Orders",
                sql: "[PointsUsed] >= 0 AND [PointsDiscountAmount] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Math",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Points",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PointsDiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PointsUsed",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Math",
                table: "Orders",
                sql: "[TotalAmount] = [SubTotal] - [DiscountAmount]");
        }
    }
}
