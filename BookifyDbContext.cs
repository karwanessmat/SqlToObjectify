using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SqlToObjectify.Models;

namespace SqlToObjectify
{
    public class BookifyDbContext:DbContext
    {

        public BookifyDbContext()
        
        {
        }
        public BookifyDbContext(DbContextOptions<BookifyDbContext> options) 
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            const string connectionStrings = "Server=.;Database=booify_Db;Integrated Security=True;Encrypt=False;";

            var conn =    optionsBuilder.UseSqlServer(connectionStrings);

            base.OnConfiguring(optionsBuilder);

        }

     


        
    }






}
