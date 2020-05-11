using Core.Models;
using Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Services
{
    public class SitecoreTreeScannerService : ISitecoreTreeScannerService
    {
        private ILogger<SitecoreTreeScannerService> _logger;
        private readonly ISitecoreProvider _sitecoreProvider;
        private readonly IPagesRepository _pagesRepository;
        private readonly IConfiguration _configuration;

        private readonly string _localMediaFolder;
        private readonly string _mediaItemBaseUrl;

        public SitecoreTreeScannerService(ISitecoreProvider sitecoreProvider,
            ILoggerFactory loggerFactory,
            IPagesRepository pagesRepository,
            IConfiguration configuration)
        {
            _configuration = configuration;
            _sitecoreProvider = sitecoreProvider;
            _pagesRepository = pagesRepository;

            _logger = loggerFactory.CreateLogger<SitecoreTreeScannerService>();

            _mediaItemBaseUrl = _configuration["MediaLibrary:Url"];
            _localMediaFolder = _configuration["MediaLibrary:LocalMediaFolder"];
        }

        public async Task<SyncModelResponse> ScanSitecoreTree(Guid rootId)
        {
            var result = new SyncModelResponse();
            try
            {
                await _sitecoreProvider.Login();
                SitecoreTreeScanModel pages = await SyncSitecorePages(rootId);
                result.ReadFromSitecoreTime = pages.Time;
                result.PagesCount = pages.Count;

                DateTime start = DateTime.UtcNow;

                await _pagesRepository.Save(pages.Pages);

                result.SaveToDBTime = DateTime.UtcNow - start;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }

            return result;
        }

        public async Task<SitecoreMediaLibraryScanModel> ScanMediaLibrary(Guid rootId)
        {
            var result = new SitecoreMediaLibraryScanModel()
            {
                Start = DateTime.UtcNow
            };

            try
            {
                var mediaLibraryItems = new Dictionary<Guid, MediaLibraryItem>();

                MediaLibraryScItem rootItem = await _sitecoreProvider.GetScItemByIdAsync<MediaLibraryScItem>(rootId, true);
                if (rootItem != null)
                {
                    var wc = new WebClient();
                    //use local media folder path as fake parent item
                    MediaLibraryItem root = ProcessMediaLibraryItem(rootItem, new MediaLibraryItem { LocalPath = _localMediaFolder, IsFolder = true }, wc);
                    mediaLibraryItems.Add(root.Id, root);

                    await ScanMediaTree(root, mediaLibraryItems, wc);
                }

                result.Count = mediaLibraryItems.Count;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }

            result.End = DateTime.UtcNow;
            result.Time = result.End - result.Start;

            return result;
        }

        private async Task ScanMediaTree(MediaLibraryItem root, Dictionary<Guid, MediaLibraryItem> mediaLibraryItems, WebClient wc)
        {
            try
            {
                var children = await _sitecoreProvider.GetScItemChildrenAsync<MediaLibraryScItem>(root.Id, true);
                foreach (var child in children)
                {
                    try
                    {
                        MediaLibraryItem mediaFile = ProcessMediaLibraryItem(child, root, wc);
                        mediaLibraryItems.Add(child.ItemID, mediaFile);

                        await ScanMediaTree(mediaFile, mediaLibraryItems, wc);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured on sitecore tree scan. " + ex.Message);
            }
        }

        private MediaLibraryItem ProcessMediaLibraryItem(MediaLibraryScItem item, MediaLibraryItem parent, WebClient wc)
        {
            var mediaFile = new MediaLibraryItem
            {
                Id = item.ItemID,
                Name = item.ItemName,
                ParentFolderPath = parent.LocalPath,
            };
            if (item.TemplateName == "File" || item.TemplateName == "Image" || item.TemplateName == "Jpeg" || item.TemplateName == "Docx")
            {
                string parentFolder = parent.LocalPath;
                if (!parent.IsFolder)
                {
                    parentFolder = CreateFolder(parent.ParentFolderPath, parent.Name);
                }

                string fileUrl = $"{_mediaItemBaseUrl}/~/media/{item.ItemID.ToString("N").ToUpper()}.ashx";
                string filePath = $"{parentFolder}\\{item.ItemName}.{item.Extension}";

                wc.DownloadFileAsync(new Uri(fileUrl), filePath);

                mediaFile.IsFolder = false;
                mediaFile.LocalPath = filePath;
                mediaFile.ParentFolderPath = parentFolder;
            }
            else
            {
                mediaFile.LocalPath = CreateFolder(parent.LocalPath, item.ItemName);
                mediaFile.IsFolder = true;
            }

            return mediaFile;
        }
               
        private async Task<SitecoreTreeScanModel> SyncSitecorePages(Guid rootId)
        {
            var result = new SitecoreTreeScanModel()
            {
                Start = DateTime.UtcNow
            };

            var sitecorePages = new Dictionary<Guid, SitecoreItemModel>();

            SitecoreItemModel item = await _sitecoreProvider.GetScItemByIdAsync<SitecoreItemModel>(rootId, true);
            if (item != null && item.IsPage)
            {
                sitecorePages.Add(item.ItemID, item);
            }
            else if (item == null)
            {
                _logger.LogError($"Root item {rootId} does not exist.");
                return result;
            }

            await TraverseSitecoreTree(rootId, sitecorePages);

            result.Pages = await ConvertSitecorePagesToDbModels(sitecorePages);
            result.Count = sitecorePages.Count;
            result.End = DateTime.UtcNow;
            result.Time = result.End - result.Start;

            return result;
        }

        /// <summary>
        /// Recursion method which build collection of sitecore pages by root id. 
        /// SitecoreItemModel contains fields which help to understand if this item is page or datasource item.
        /// </summary>
        /// <param name="rootId"></param>
        /// <param name="pages"></param>
        /// <returns></returns>
        private async Task TraverseSitecoreTree(Guid rootId, Dictionary<Guid, SitecoreItemModel> sitecorePages)
        {
            try
            {
                var children = await _sitecoreProvider.GetScItemChildrenAsync<SitecoreItemModel>(rootId, true);
                foreach (var child in children)
                {
                    if (child.IsPage)
                    {
                        sitecorePages.Add(child.ItemID, child);
                    }

                    if (child.HasChildren)
                    {
                        await TraverseSitecoreTree(child.ItemID, sitecorePages);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured on sitecore tree scan. " + ex.Message);
            }
        }

        /// <summary>
        /// Send API request to sitecore to get page data (fields). 
        /// Then parse renderings field to gather datasources ids. 
        /// Then send request to get datasource json.
        /// </summary>
        /// <param name="pages"></param>
        /// <returns></returns>
        private async Task<List<SitecorePageDbModel>> ConvertSitecorePagesToDbModels(Dictionary<Guid, SitecoreItemModel> sitecorePages)
        {
            var result = new List<SitecorePageDbModel>();
            foreach (var pagePair in sitecorePages)
            {
                var dbModel = new SitecorePageDbModel()
                {
                    Id = pagePair.Key,
                    ParentId = pagePair.Value.ParentID,
                    TemplateName = pagePair.Value.TemplateName
                };

                var itemsList = new List<string>();
                try
                {
                    dbModel.Page = await _sitecoreProvider.GetScItemJsonByIdAsync(pagePair.Key);
                    List<Guid> datasources = ParseRenderingDatasources(pagePair.Value.Renderings + pagePair.Value.FinalRenderings);
                    foreach (var datasourceId in datasources)
                    {
                        try
                        {
                            string item = await _sitecoreProvider.GetScItemJsonByIdAsync(datasourceId);
                            if (!string.IsNullOrEmpty(item))
                            {
                                itemsList.Add(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.Message);
                        }
                    }

                    dbModel.Items = JsonSerializer.Serialize(itemsList);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }

                result.Add(dbModel);
            }

            return result;
        }

        private string CreateFolder(string rootPath, string folderName)
        {
            string folderPath = $"{rootPath}\\{folderName}";

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            return folderPath;
        }

        private List<Guid> ParseRenderingDatasources(string renderings)
        {
            var result = new List<Guid>();
            var matches = Regex.Matches(renderings, "ds=\"{(.*?)}");
            foreach (var match in matches)
            {
                Guid id;
                string guidStr = ((Match)match)?.Groups[1]?.Value;
                if (Guid.TryParse(guidStr, out id))
                {
                    result.Add(id);
                }
            }

            return result;
        }
    }
}
