using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace WattEngine.Ideask.Task;

/// <summary>Allocates immutable task numbers atomically per board.</summary>
public class TaskSequenceService(AppDatabase db)
{
    public async System.Threading.Tasks.Task<int> AllocateAsync(Guid broadId, CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE broads
                SET next_task_number = next_task_number + 1
                WHERE id = @broad_id
                RETURNING next_task_number - 1;
                """;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            var broadIdParameter = command.CreateParameter();
            broadIdParameter.ParameterName = "broad_id";
            broadIdParameter.Value = broadId;
            command.Parameters.Add(broadIdParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is int number
                ? number
                : throw new KeyNotFoundException("Broad not found while allocating a task number.");
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
