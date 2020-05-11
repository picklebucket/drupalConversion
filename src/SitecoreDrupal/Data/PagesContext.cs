using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Data
{
    public class PagesContext : DbContext
    {
        public PagesContext(DbContextOptions<PagesContext> options) : base(options)
        {
        }

        public DbSet<SitecorePageDbModel> Pages { get; set; }
    }
}
