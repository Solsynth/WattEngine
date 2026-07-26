using Microsoft.EntityFrameworkCore;

namespace WattEngine.Ideask.Task;

/// <summary>Allocates immutable task numbers atomically per board.</summary>
public class TaskSequenceService(AppDatabase db)
{
    public async System.Threading.Tasks.Task<int> AllocateAsync(Guid broadId, CancellationToken cancellationToken = default)
    {
        return await db.Database.SqlQuery<int>($"""
            UPDATE broads
            SET next_task_number = next_task_number + 1
            WHERE id = {broadId}
            RETURNING next_task_number - 1 AS "Value"
            """).SingleAsync(cancellationToken);
    }
}
