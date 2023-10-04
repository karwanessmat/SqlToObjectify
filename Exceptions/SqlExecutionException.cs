namespace SqlToObjectify.Exceptions
{
    public class SqlExecutionException : Exception
    {
        public SqlExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

}
