using DysonNetwork.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WattEngine.Flywheel.Flywheel;

namespace WattEngine.Flywheel;

public class AppDatabase(DbContextOptions<AppDatabase> options, IConfiguration configuration) : DbContext(options)
{
    public DbSet<FlywheelAppSettings> AppSettings => Set<FlywheelAppSettings>();
    public DbSet<FlywheelBlob> Blobs => Set<FlywheelBlob>();
    public DbSet<FlywheelBlobRevision> BlobRevisions => Set<FlywheelBlobRevision>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("App"), opt =>
            opt.ConfigureDataSource(source => source.EnableDynamicJson()).UseNodaTime())
            .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlywheelAppSettings>().HasIndex(x => new { x.WorkspaceId, x.AppId }).IsUnique();
        modelBuilder.Entity<FlywheelBlob>().HasIndex(x => new { x.WorkspaceId, x.AppId, x.BlobId }).IsUnique();
        modelBuilder.Entity<FlywheelBlobRevision>().HasIndex(x => new { x.BlobId, x.Revision }).IsUnique();
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
