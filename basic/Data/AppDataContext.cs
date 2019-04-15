using Microsoft.Data.Entity;
using Microsoft.Extensions.Configuration;
using TodoCore.Models;

namespace TodoCore.Data
{
    public class AppDataContext : DbContext
    {
        public DbSet<TodoList> TodoLists { get; set; }
        public DbSet<TodoItem> TodoItems { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase();
        }
    }
}