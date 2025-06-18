
using System.ComponentModel.DataAnnotations;

namespace SqlToObjectify.ViewModels;

public sealed class ProjectTimelinePagedRaw
{
    public Guid ProjectId { get; set; }
    public Guid TypeId { get; set; }
    public string RecordType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Text { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public ProjectTaskStatus? TaskStatus { get; set; }
    public TaskPriority? TaskPriority { get; set; }
    public string? ActivityCategory { get; set; }
    public string? MilestoneName { get; set; }
    public decimal? TransactionAmount { get; set; }
    public string? CurrencySymbol { get; set; }
    public ProjectTransactionType? TransactionType { get; set; }
    public string? FileList { get; set; } // e.g. "url1|url2|url3"
}



public sealed record ProjectTimelineResponse
(
    Guid ProjectId,
    Guid TypeId,
    string RecordType,
    string Title,
    string? Text,
    DateTimeOffset CreatedAt,
    ProjectTaskStatus? TaskStatus,
    TaskPriority? TaskPriority,
    string? Category,
    string? MilestoneName,
    decimal? TransactionAmount,
    ProjectTransactionType? TransactionType,
    string? CurrencySymbol,
    List<File> Files
);

public sealed record File(string FilePath);





public class ProjectTimelineTestResponse
{
    public Guid ProjectId { get; set; }
    public Guid TypeId { get; set; }
    public string RecordType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Text { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}



public enum TaskPriority
{
    [Display(Name = "PriorityNormal")]
    Normal,

    [Display(Name = "PriorityMedium")]
    Medium,

    [Display(Name = "PriorityHigh")]
    High,

    [Display(Name = "PriorityCritical")]
    Critical
}


public enum ProjectTaskStatus
{
    Pending,
    Started,
    Completed,
    Cancelled
}


public enum ProjectTransactionType
{
    CashIn,
    CashOut,
}