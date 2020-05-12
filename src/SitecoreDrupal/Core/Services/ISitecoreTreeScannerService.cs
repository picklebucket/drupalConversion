using Core.Models;
using System;
using System.Threading.Tasks;

namespace Core.Services
{
    public interface ISitecoreTreeScannerService
    {
        Task<SyncModelResponse> ScanSitecoreTree(Guid contentRootId, Guid mediaLibraryRootId);

        Task<SitecoreMediaLibraryScanModel> ScanMediaLibrary(Guid rootId);
    }
}
