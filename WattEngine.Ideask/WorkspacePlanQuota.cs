namespace WattEngine.Ideask;

public enum WorkspacePlan
{
    Free = 0,
    Pro = 1,
    Enterprise = 2
}

public static class WorkspacePlanQuota
{
    public static int GetMaxBroadsPerProject(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 5,
        WorkspacePlan.Pro => 50,
        WorkspacePlan.Enterprise => 200,
        _ => 5
    };

    public static int GetMaxTasksPerProject(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 100,
        WorkspacePlan.Pro => 1000,
        WorkspacePlan.Enterprise => 10000,
        _ => 100
    };
}
