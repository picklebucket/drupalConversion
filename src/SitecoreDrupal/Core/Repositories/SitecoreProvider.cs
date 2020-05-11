using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public class SitecoreProvider : ISitecoreProvider
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _configuration;

        private readonly ILogger<SitecoreProvider> _logger;

        private HttpClient _httpClient;
        private string _baseScUrl;

        private DateTime _loginTime;

        public SitecoreProvider(IHttpClientFactory clientFactory, ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
            _clientFactory = clientFactory;
            _configuration = configuration;

            _httpClient = _clientFactory.CreateClient();
            _baseScUrl = _configuration["SitecoreAPI:Url"];
            _logger = loggerFactory.CreateLogger<SitecoreProvider>();
        }

        public async Task<SitecoreItemModel> GetScItemByPathAsync(string path, bool isIncludeStdFields = false)
        {
            await this.ReAuth();

            string includeStdFieldsParam = isIncludeStdFields ? "&includeStandardTemplateFields=true" : "";
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                _baseScUrl + $"/item/?path={path}" + includeStdFieldsParam);

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            string contentString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var item = JsonConvert.DeserializeObject<SitecoreItemModel>(contentString);
                return item;
            }
            else
            {
                throw new Exception($"Error occured on get children request. Item Path: {path}. " + contentString);
            }
        }

        public async Task<T> GetScItemByIdAsync<T>(Guid id, bool isIncludeStdFields = false) where T : class
        {
            string contentString = await GetScItemContentByIdAsync(id, isIncludeStdFields);
            var item = JsonConvert.DeserializeObject<T>(contentString);
            return item;
        }

        public async Task<string> GetScItemJsonByIdAsync(Guid id, bool isIncludeStdFields = false)
        {
            string contentString = await GetScItemContentByIdAsync(id, isIncludeStdFields);
            return contentString;
        }
        
        public async Task<IReadOnlyList<T>> GetScItemChildrenAsync<T>(Guid id, bool isIncludeStdFields = false) where T : class
        {
            await this.ReAuth();

            string includeStdFieldsParam = isIncludeStdFields ? "?includeStandardTemplateFields=true" : "";
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                _baseScUrl + $"/item/{id.ToString("D")}/children" + includeStdFieldsParam);

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            string contentString = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var items = JsonConvert.DeserializeObject<List<T>>(contentString);
                return items;
            }
            else
            {
                throw new Exception($"Error occured on get children request. Item Id: {id.ToString()}. Status code: {response.StatusCode}");
            }
        }

        public async Task Login()
        {
            string baseUrl = _configuration["SitecoreAPI:Url"];

            dynamic json = new ExpandoObject();
            json.domain = _configuration["SitecoreAPI:Credentials:Domain"];
            json.username = _configuration["SitecoreAPI:Credentials:User"];
            json.password = _configuration["SitecoreAPI:Credentials:Password"];

            var content = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(baseUrl + "/auth/login", content);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Error occured on login to sitecore. " + await response.Content.ReadAsStringAsync());
            }

            _loginTime = DateTime.UtcNow;
        }

        private async Task ReAuth()
        {
            if (_loginTime == DateTime.MinValue || (DateTime.UtcNow - _loginTime).Minutes <= 10)
            {
                await this.Login();
            }
        }

        private async Task<string> GetScItemContentByIdAsync(Guid id, bool isIncludeStdFields)
        {
            await this.ReAuth();

            string includeStdFieldsParam = isIncludeStdFields ? "?includeStandardTemplateFields=true" : "";
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                _baseScUrl + $"/item/{id.ToString("D")}" + includeStdFieldsParam);

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            string contentString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return string.Empty;
                }
                else
                {
                    throw new Exception($"Error occured on get children request. Item Path: {id}. " + contentString);
                }
            }

            return contentString;
        }
    }
}
