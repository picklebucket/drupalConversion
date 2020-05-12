using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Core.Repositories;
using Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SitecoreDrupal.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ISitecoreTreeScannerService _sitecoreTreeScannerService;

        public TestController(ISitecoreTreeScannerService sitecoreTreeScannerService)
        {
            _sitecoreTreeScannerService = sitecoreTreeScannerService;
        }

        [HttpGet("sync")]
        public async Task<ActionResult<SyncModelResponse>> Sync(Guid contentRoot, Guid mediaLibraryRoot)
        {
            return Ok(JsonConvert.SerializeObject(await _sitecoreTreeScannerService.ScanSitecoreTree(contentRoot, mediaLibraryRoot)));
        }

        [HttpGet("syncmedia")]
        public async Task<ActionResult<SyncModelResponse>> SyncMedia(Guid id)
        {
            return Ok(JsonConvert.SerializeObject(await _sitecoreTreeScannerService.ScanMediaLibrary(id)));
        }
    }
}
