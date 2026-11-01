using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OctopusMES.Api.Persistence;

namespace OctopusMES.Api.Tests;

public sealed class ResourceCalendarHorizonTests
{
    [Fact]
    public async Task Extends_existing_resource_calendar_through_rolling_horizon()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MesDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new MesDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var resource = new MesResource
        {
            ResourceCode = "RES-01", ResourceName = "装配线", ResourceKind = "equipment",
            CapabilityCodes = "CAP-ASM", Status = "available"
        };
        db.Resources.Add(resource);
        db.SaveChanges();
        db.ResourceCalendars.Add(new MesResourceCalendar
        {
            ResourceId = resource.ResourceId, WorkDate = new DateTime(2026, 8, 1),
            IsAvailable = true, IsMaintenance = false,
            AvailableFrom = TimeSpan.FromHours(8), AvailableTo = TimeSpan.FromHours(17)
        });
        db.SaveChanges();

        var added = MesDbSeeder.EnsureRollingResourceCalendars(db, new DateTime(2026, 8, 7), 7);

        added.Should().Be(8);
        db.ResourceCalendars.Should().Contain(x => x.WorkDate == new DateTime(2026, 8, 14));
        db.ResourceCalendars.Single(x => x.WorkDate == new DateTime(2026, 8, 7)).AvailableFrom
            .Should().Be(TimeSpan.FromHours(8));
    }
}
