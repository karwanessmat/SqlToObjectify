namespace SqlToObjectify.BookifyViewModels;

internal sealed record OutboxMessageResponse
{
    public Guid Id { get; set; }
    public string Content
    {
        get;
        set;
    }
};
