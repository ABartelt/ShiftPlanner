using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muddi.ShiftPlanner.Server.Database.Migrations
{
    /// <summary>
    /// Adds <c>ApplicationSettings.PublicBrowsingEnabled</c>. The settings live in one jsonb column, so there is no
    /// schema change; we only backfill the new key as <c>false</c> on the existing row so public browsing stays off
    /// for every deployment until a super-admin turns it on.
    /// </summary>
    public partial class AddPublicBrowsingSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE app_settings
                SET settings = settings || '{"PublicBrowsingEnabled": false}'::jsonb
                WHERE NOT (settings ? 'PublicBrowsingEnabled');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE app_settings SET settings = settings - 'PublicBrowsingEnabled';");
        }
    }
}
