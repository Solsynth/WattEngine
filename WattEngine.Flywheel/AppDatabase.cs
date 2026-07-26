using DysonNetwork.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WattEngine.Flywheel.Flywheel;

namespace WattEngine.Flywheel;

public class AppDatabase(DbContextOptions<AppDatabase> options, IConfiguration configuration) : DbContext(options)
{
    public DbSet<FlywheelStream> Streams => Set<FlywheelStream>();
    public DbSet<FlywheelDevice> Devices => Set<FlywheelDevice>();
    public DbSet<FlywheelOperation> Operations => Set<FlywheelOperation>();
    public DbSet<FlywheelStreamMember> StreamMembers => Set<FlywheelStreamMember>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("App"), opt =>
            opt.ConfigureDataSource(source => source.EnableDynamicJson()).UseNodaTime())
            .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlywheelStream>().HasIndex(x => new { x.WorkspaceId, x.AppId }).IsUnique();
        modelBuilder.Entity<FlywheelDevice>().HasIndex(x => new { x.StreamId, x.DeviceId }).IsUnique();
        modelBuilder.Entity<FlywheelOperation>().HasIndex(x => new { x.StreamId, x.OperationId }).IsUnique();
        modelBuilder.Entity<FlywheelOperation>().HasIndex(x => new { x.StreamId, x.Cursor }).IsUnique();
        modelBuilder.Entity<FlywheelStreamMember>().HasIndex(x => new { x.StreamId, x.AccountId }).IsUnique();
    }
}

public class AppDatabaseFactory : IDesignTimeDbContextFactory<AppDatabase>
{
    public AppDatabase CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json").Build();
        return new AppDatabase(new DbContextOptionsBuilder<AppDatabase>().Options, configuration);
    }
}
