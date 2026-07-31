using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceRequest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestCommentCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RequestComments_CreatedAt",
                table: "RequestComments",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestComments_CreatedAt",
                table: "RequestComments");
        }
    }
}
