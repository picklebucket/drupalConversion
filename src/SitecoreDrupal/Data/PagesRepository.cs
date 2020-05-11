using Core.Models;
using Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data
{
    public class PagesRepository : IPagesRepository
    {
        private PagesContext _context;

        public PagesRepository(PagesContext context)
        {
            _context = context;
        }

        public async Task Save(List<SitecorePageDbModel> pages)
        {
            if (pages.Count > 0)
            {
                _context.Database.ExecuteSqlRaw("delete from Pages");

                foreach (var page in pages)
                {
                    _context.Pages.Add(page);
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
