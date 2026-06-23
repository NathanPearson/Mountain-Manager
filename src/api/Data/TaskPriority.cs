namespace MountainManager.Api.Data;

public sealed class TaskPriority
{
    public const int LowId = 1;
    public const int MediumId = 2;
    public const int HighId = 3;
    public const int UrgentId = 4;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortRank { get; set; }
}
