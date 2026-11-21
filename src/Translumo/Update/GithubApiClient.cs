using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Translumo.Utils.Http;

namespace Translumo.Update
{
    public class GithubApiClient : IReleasesClient
    {
        public string Owner { get; set; }

        public string Repository { get; set; }


        private readonly HttpReader _reader;

        private const string BASE_API_URL = "https://api.github.com";

        public GithubApiClient(string owner, string repository)
        {
            this.Owner = owner;
            this.Repository = repository;
            this._reader = new HttpReader();
            _reader.Accept = "application/vnd.github+json";
        }

        public async Task<Version> GetLastVersionAsync()
        {
            var releases = await GetReleasesAsync();
            if (releases == null || !releases.Any())
            {
                return null;
            }

            var validVersions = new System.Collections.Generic.List<Version>();
            foreach (var release in releases)
            {
                if (release.Draft || string.IsNullOrEmpty(release.TagName))
                {
                    continue;
                }

                var cleanTag = release.TagName.TrimStart('v', 'V', ' ', '.');
                cleanTag = cleanTag.Split('-', '+')[0].Trim();

                if (Version.TryParse(cleanTag, out var version))
                {
                    validVersions.Add(version);
                }
            }

            return validVersions.Any() ? validVersions.Max() : null;
        }

        private async Task<ReleaseItemResponse[]> GetReleasesAsync()
        {
            var targetUrl = $"{BASE_API_URL}/repos/{Owner}/{Repository}/releases";
            var response = await _reader.RequestWebDataAsync(targetUrl, HttpMethods.GET);
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException($"Failed to receive Version from Github", response.InnerException);
            }

            return JsonSerializer.Deserialize<ReleaseItemResponse[]>(response.Body);
        }
    }
}
