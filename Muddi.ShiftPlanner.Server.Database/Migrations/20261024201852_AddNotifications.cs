using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muddi.ShiftPlanner.Server.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_channel_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_keycloak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unsubscribe_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_channel_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_link_codes",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    employee_keycloak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_link_codes", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    employee_keycloak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remind_day_before = table.Column<bool>(type: "boolean", nullable: false),
                    remind_short_before = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.employee_keycloak_id);
                });

            migrationBuilder.CreateTable(
                name: "shift_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_keycloak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    shift_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    action_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    action_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    release_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    action_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shift_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_shift_notifications_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_channel_links_channel_address",
                table: "notification_channel_links",
                columns: new[] { "channel", "address" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_channel_links_employee_keycloak_id_channel",
                table: "notification_channel_links",
                columns: new[] { "employee_keycloak_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_channel_links_unsubscribe_token",
                table: "notification_channel_links",
                column: "unsubscribe_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shift_notifications_action_token",
                table: "shift_notifications",
                column: "action_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shift_notifications_channel_address",
                table: "shift_notifications",
                columns: new[] { "channel", "address" });

            migrationBuilder.CreateIndex(
                name: "ix_shift_notifications_created_at",
                table: "shift_notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_shift_notifications_shift_id_employee_keycloak_id_kind_shif",
                table: "shift_notifications",
                columns: new[] { "shift_id", "employee_keycloak_id", "kind", "shift_start", "channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_channel_links");

            migrationBuilder.DropTable(
                name: "notification_link_codes");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "shift_notifications");
        }
    }
}
