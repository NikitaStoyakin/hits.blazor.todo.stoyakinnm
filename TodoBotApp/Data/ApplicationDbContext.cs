using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoBotApp.Data.Models; 

namespace TodoBotApp.Data 
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Intent> Intents { get; set; }
        public DbSet<ExpertQuestion> ExpertQuestions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Intent>()
                .Ignore(i => i.Patterns)
                .Ignore(i => i.Responses);
        }

    }
}
