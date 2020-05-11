using Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public interface ISitecoreProvider
    {
        Task<SitecoreItemModel> GetScItemByPathAsync(string path, bool isIncludeStdFields = false);

        Task<T> GetScItemByIdAsync<T>(Guid id, bool isIncludeStdFields = false) where T : class;

        Task<IReadOnlyList<T>> GetScItemChildrenAsync<T>(Guid id, bool isIncludeStdFields = false) where T : class;

        Task<string> GetScItemJsonByIdAsync(Guid id, bool isIncludeStdFields = false);

        Task Login();
    }
}
