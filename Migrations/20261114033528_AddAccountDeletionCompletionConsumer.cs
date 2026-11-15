using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foll_IAM.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionCompletionConsumer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_account_deletion_requests_user_user_id",
                schema: "iam",
                table: "account_deletion_requests");

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at_utc",
                schema: "iam",
                table: "account_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "integration_inbox_messages",
                schema: "iam",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_inbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_inbox_messages_operation_id",
                schema: "iam",
                table: "integration_inbox_messages",
                column: "operation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_inbox_messages",
                schema: "iam");

            migrationBuilder.DropColumn(
                name: "completed_at_utc",
                schema: "iam",
                table: "account_deletion_requests");

            migrationBuilder.AddForeignKey(
                name: "fk_account_deletion_requests_user_user_id",
                schema: "iam",
                table: "account_deletion_requests",
                column: "user_id",
                principalSchema: "iam",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
