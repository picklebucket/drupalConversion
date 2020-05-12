using Core.Models;
using Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

        public async Task<SyncModelResponse> ScanSitecoreTree(Guid contentRootId, Guid mediaLibraryRootId)
        {
            var result = new SyncModelResponse();
            try
            {
                await _sitecoreProvider.Login();
                SitecoreMediaLibraryScanModel mediItems = await ScanMediaLibrary(mediaLibraryRootId);
                SitecoreTreeScanModel pages = await SyncSitecorePages(contentRootId, mediItems.MediaItems);
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
                result.MediaItems = mediaLibraryItems;
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

        private async Task<SitecoreTreeScanModel> SyncSitecorePages(Guid rootId, Dictionary<Guid, MediaLibraryItem> mediaItems)
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

            result.Pages = await ConvertSitecorePagesToDbModels(sitecorePages, mediaItems);
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
        private async Task<List<SitecorePageDbModel>> ConvertSitecorePagesToDbModels(
            Dictionary<Guid, SitecoreItemModel> sitecorePages, Dictionary<Guid, MediaLibraryItem> mediaItems)
        {
            var result = new List<SitecorePageDbModel>();
            var cacheItems = new Dictionary<Guid, object>();
            foreach (var pagePair in sitecorePages)
            {
                var dbModel = new SitecorePageDbModel()
                {
                    Id = pagePair.Key,
                    ParentId = pagePair.Value.ParentID,
                    TemplateName = pagePair.Value.TemplateName
                };

                var itemsList = new List<object>();
                try
                {
                    string pageJson = await _sitecoreProvider.GetScItemJsonByIdAsync(pagePair.Key);
                    var pageJsonObj = JsonConvert.DeserializeObject<object>(pageJson);
                    dbModel.Page = JsonConvert.SerializeObject(pageJsonObj);
                    dbModel.Page = await UpdateNestedIds(dbModel.Page, cacheItems);
                    dbModel.Page = await UpdateImages(dbModel.Page, mediaItems);
                    List<Guid> datasources = ParseRenderingDatasources(pagePair.Value.Renderings + pagePair.Value.FinalRenderings);
                    foreach (var datasourceId in datasources)
                    {
                        try
                        {
                            string item = await _sitecoreProvider.GetScItemJsonByIdAsync(datasourceId);
                            if (!string.IsNullOrEmpty(item))
                            {
                                itemsList.Add(JsonConvert.DeserializeObject<object>(item));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.Message);
                        }
                    }

                    dbModel.Items = JsonConvert.SerializeObject(itemsList);
                    dbModel.Items = await UpdateNestedIds(dbModel.Items, cacheItems);
                    dbModel.Items = await UpdateImages(dbModel.Items, mediaItems);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }

                result.Add(dbModel);
            }

            return result;
        }

        private async Task<string> UpdateImages(string itemJson, Dictionary<Guid, MediaLibraryItem> mediaFiles)
        {
            List<KeyValuePair<string, Guid>> imageMappings = GetMediaItemsGuids(itemJson);
            foreach (var imageMapping in imageMappings)
            {
                MediaLibraryItem mediaFile;
                if (mediaFiles.TryGetValue(imageMapping.Value, out mediaFile))
                {
                    itemJson = itemJson.Replace(imageMapping.Key, mediaFile.LocalPath);
                }
            }

            return itemJson;
        }

        private async Task<string> UpdateNestedIds(string itemJson, Dictionary<Guid, object> cache)
        {
            var guidsMappings = GetGuids(itemJson);

            if (guidsMappings.Count == 0)
                return itemJson;

            foreach (var guidMap in guidsMappings)
            {
                var items = new List<object>();
                foreach (var guid in guidMap.Value)
                {
                    if (cache.ContainsKey(guid))
                    {
                        items.Add(cache[guid]);
                    }
                    else
                    {
                        string scItemJson = await _sitecoreProvider.GetScItemJsonByIdAsync(guid);
                        object scItem = JsonConvert.DeserializeObject<object>(scItemJson);
                        cache.Add(guid, scItem);
                        items.Add(scItem);
                    }
                }

                if (items.Count == 1)
                {
                    itemJson = itemJson.Replace(guidMap.Key, JsonConvert.SerializeObject(items[0]));
                }
                else
                {
                    itemJson = itemJson.Replace(guidMap.Key, JsonConvert.SerializeObject(items));
                }
            }

            return await UpdateNestedIds(itemJson, cache);
        }

        private List<KeyValuePair<string, Guid>> GetMediaItemsGuids(string itemJson)
        {
            //mediaid=.*?({.*?})
            //("<image.*?\/>")
            var result = new List<KeyValuePair<string, Guid>>();
            var imageFieldMatches = Regex.Matches(itemJson, "(<image.*?/>)");
            foreach (var imageFieldMatche in imageFieldMatches)
            {
                string imageFieldValue = ((Match)imageFieldMatche)?.Groups[1]?.Value;
                if (!string.IsNullOrEmpty(imageFieldValue))
                {
                    var imageIdMatche = Regex.Match(imageFieldValue, "mediaid=.*?({.*?})");
                    string imageIdValue = ((Match)imageIdMatche)?.Groups[1]?.Value;
                    Guid imageId;
                    if (!string.IsNullOrEmpty(imageIdValue) && Guid.TryParse(imageIdValue, out imageId))
                    {
                        result.Add(new KeyValuePair<string, Guid>(imageFieldValue, imageId));
                    }
                }
            }

            return result;
        }

        private List<KeyValuePair<string, List<Guid>>> GetGuids(string itemJson)
        {
            var result = new List<KeyValuePair<string, List<Guid>>>();
            var idFieldsMatches = Regex.Matches(itemJson, ":*(\"{.*?}\"),");
            foreach (var match in idFieldsMatches)
            {
                string guidStrMatch = ((Match)match)?.Groups[1]?.Value;
                if (!string.IsNullOrEmpty(guidStrMatch))
                {
                    guidStrMatch = guidStrMatch.Replace("{", "");
                    guidStrMatch = guidStrMatch.Replace("}", "");
                    guidStrMatch = guidStrMatch.Replace("}", "");
                    guidStrMatch = guidStrMatch.Replace("}", "");
                    guidStrMatch = guidStrMatch.Replace("\"", "");
                    var guids = guidStrMatch.Split('|');
                    var ids = new List<Guid>();
                    foreach (var guid in guids)
                    {
                        Guid id;
                        if (Guid.TryParse(guid, out id))
                        {
                            ids.Add(id);
                        }
                    }

                    if (ids.Count > 0)
                    {
                        result.Add(new KeyValuePair<string, List<Guid>>(((Match)match)?.Groups[1]?.Value, ids));
                    }
                }
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
