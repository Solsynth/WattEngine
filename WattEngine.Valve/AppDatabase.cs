using System.Linq.Expressions;
using DysonNetwork.Shared.Data;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NodaTime;
using Quartz;
using WattEngine.Valve.Workspace;

namespace WattEngine.Valve;

public class AppDatabase(
    DbContextOptions<AppDatabase> options,
    IConfiguration configuration
) : DbContext(options)
{
    public DbSet<WtWorkspace> Workspaces { get; set; } = null!;
    public DbSet<WtWorkspaceMember> WorkspaceMembers { get; set; } = null!;
    public DbSet<WtWorkspaceRolePermission> WorkspaceRolePermissions { get; set; } = null!;
    public DbSet<WtWorkspaceUserPermission> WorkspaceUserPermissions { get; set; } = null!;
    public DbSet<WtWorkspaceBundledPlan> WorkspaceBundledPlans { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(
            configuration.GetConnectionString("App"),
            opt => opt
                .ConfigureDataSource(optSource => optSource.EnableDynamicJson())
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                .UseNodaTime()
        ).UseSnakeCaseNamingConvention();

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplySoftDeleteFilters();

        // WtWorkspace
        modelBuilder.Entity<WtWorkspace>()
            .HasMany(w => w.Members)
            .WithOne(m => m.Workspace)
            .HasForeignKey(m => m.WorkspaceId);

        modelBuilder.Entity<WtWorkspace>()
            .HasMany(w => w.RolePermissions)
            .WithOne(p => p.Workspace)
            .HasForeignKey(p => p.WorkspaceId);

        modelBuilder.Entity<WtWorkspace>()
            .HasMany(w => w.UserPermissions)
            .WithOne(p => p.Workspace)
            .HasForeignKey(p => p.WorkspaceId);

        modelBuilder.Entity<WtWorkspace>()
            .HasMany(w => w.BundledPlans)
            .WithOne(b => b.Workspace)
            .HasForeignKey(b => b.WorkspaceId);

        // Unique indexes
        modelBuilder.Entity<WtWorkspaceRolePermission>()
            .HasIndex(p => new { p.WorkspaceId, p.RoleLevel })
            .IsUnique();

        modelBuilder.Entity<WtWorkspaceUserPermission>()
            .HasIndex(p => new { p.WorkspaceId, p.AccountId })
            .IsUnique();

        modelBuilder.Entity<WtWorkspaceMember>()
            .HasIndex(m => new { m.WorkspaceId, m.AccountId })
            .IsUnique();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        this.ApplyAuditableAndSoftDelete();
        return await base.SaveChangesAsync(cancellationToken);
    }
}

public class AppDatabaseRecyclingJob(AppDatabase db, ILogger<AppDatabaseRecyclingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var now = SystemClock.Instance.GetCurrentInstant();

        logger.LogInformation("Deleting soft-deleted records...");

        var threshold = now - Duration.FromDays(7);

        var entityTypes = db.Model.GetEntityTypes()
            .Where(t => typeof(ModelBase).IsAssignableFrom(t.ClrType) && t.ClrType != typeof(ModelBase))
            .Select(t => t.ClrType);

        foreach (var entityType in entityTypes)
        {
            var set = (IQueryable)db.GetType().GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(entityType).Invoke(db, null)!;
            var parameter = Expression.Parameter(entityType, "e");
            var property = Expression.Property(parameter, nameof(ModelBase.DeletedAt));
            var condition = Expression.LessThan(property, Expression.Constant(threshold, typeof(Instant?)));
            var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(Instant?)));
            var finalCondition = Expression.AndAlso(notNull, condition);
            var lambda = Expression.Lambda(finalCondition, parameter);

            var queryable = set.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable),
                    "Where",
                    [entityType],
                    set.Expression,
                    Expression.Quote(lambda)
                )
            );

            var toListAsync = typeof(EntityFrameworkQueryableExtensions)
                .GetMethod(nameof(EntityFrameworkQueryableExtensions.ToListAsync))!
                .MakeGenericMethod(entityType);

            var items = await (dynamic)toListAsync.Invoke(null, [queryable, CancellationToken.None])!;
            db.RemoveRange(items);
        }

        await db.SaveChangesAsync();
    }
}

public class AppDatabaseFactory : IDesignTimeDbContextFactory<AppDatabase>
{
    public AppDatabase CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDatabase>();
        return new AppDatabase(optionsBuilder.Options, configuration);
    }
}
