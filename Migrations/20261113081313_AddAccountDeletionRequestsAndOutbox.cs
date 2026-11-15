using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foll_IAM.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionRequestsAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                schema: "iam",
                columns: table => new
                {
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_deletion_requests", x => x.operation_id);
                    table.ForeignKey(
                        name: "fk_account_deletion_requests_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "iam",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_outbox_messages",
                schema: "iam",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_outbox_messages", x => x.event_id);
                    table.ForeignKey(
                        name: "fk_integration_outbox_messages_account_deletion_requests_opera",
                        column: x => x.operation_id,
                        principalSchema: "iam",
                        principalTable: "account_deletion_requests",
                        principalColumn: "operation_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_requests_user_id",
                schema: "iam",
                table: "account_deletion_requests",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_outbox_messages_next_attempt_at_utc",
                schema: "iam",
                table: "integration_outbox_messages",
                column: "next_attempt_at_utc",
                filter: "published_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_integration_outbox_messages_operation_id",
                schema: "iam",
                table: "integration_outbox_messages",
                column: "operation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_outbox_messages",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "account_deletion_requests",
                schema: "iam");
        }
    }
}
