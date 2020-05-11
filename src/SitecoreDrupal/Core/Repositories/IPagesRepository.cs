using Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public interface IPagesRepository
    {
        Task Save(List<SitecorePageDbModel> pages);
    }
}
