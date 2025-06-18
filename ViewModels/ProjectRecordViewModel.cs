namespace SqlToObjectify.ViewModels;

public class ProjectRecordViewModel
{
    public Guid ProjectId { get; set; }
    public Guid TypeId { get; set; }
    public string RecordType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Text { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public string? TaskStatus { get; set; }
    public string? TaskPriority { get; set; }
    public string? ActivityCategory { get; set; }
    public string? MilestoneName { get; set; }
    public string? TransactionAmount { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? TransactionType { get; set; }
    public string? FileList { get; set; }
}
