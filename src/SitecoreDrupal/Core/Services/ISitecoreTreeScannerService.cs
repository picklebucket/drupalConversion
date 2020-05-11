using Core.Models;
using System;
using System.Threading.Tasks;

namespace Core.Services
{
    public interface ISitecoreTreeScannerService
    {
        Task<SyncModelResponse> ScanSitecoreTree(Guid rootId);

        Task<SitecoreMediaLibraryScanModel> ScanMediaLibrary(Guid rootId);
    }
}
