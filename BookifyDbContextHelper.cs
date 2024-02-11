using Microsoft.EntityFrameworkCore;
using SqlToObjectify.BookifyViewModels;
using SqlToObjectify.ViewModels;

namespace SqlToObjectify
{
    internal class BookifyDbContextHelper
    {


        public async Task Invoice()
        {
            var dbContext = new BookifyDbContext();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var outboxMessages = await GetOutboxMessagesAsync(dbContext);

            foreach (var outboxMessage in outboxMessages)
            {
                Exception? exception = null;


                await UpdateOutboxMessageAsync(dbContext, outboxMessage, exception);
            }

            await transaction.CommitAsync();
        }

        async Task<List<OutboxMessageResponse>> GetOutboxMessagesAsync(BookifyDbContext dbContext)
        {
            var sql = $"""
                       SELECT TOP (10) Id, Content
                       FROM outbox_messages WITH (UPDLOCK, READPAST)
                       WHERE ProcessesOnUtc IS NULL
                       ORDER BY OccurredOnUtc
                       """;
            var result = await dbContext.Database.SqlQueryRaw<OutboxMessageResponse>(sql).AsNoTracking().ToListAsync();

            var outboxMessages = await dbContext
                .SelectSqlQueryListAsync<OutboxMessageResponse>(sql);


        
            return outboxMessages!;


        }


        async Task UpdateOutboxMessageAsync(BookifyDbContext dbContext,OutboxMessageResponse outboxMessage, Exception? exception)
        {
            var errorInfo = exception?.ToString() ?? string.Empty;
            // Truncate error info if necessary to fit into your database schema constraints

            await dbContext.Database.ExecuteSqlRawAsync("""
                                                        UPDATE outbox_messages WITH (UPDLOCK, READPAST)
                                                        SET ProcessesOnUtc = {0}, error = {1}
                                                        WHERE id = {2}
                                                        """,
                DateTime.UtcNow, errorInfo, outboxMessage.Id);
        }


    }
}
