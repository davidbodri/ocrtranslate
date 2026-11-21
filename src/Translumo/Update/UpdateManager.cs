using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Translumo.Infrastructure.Constants;

namespace Translumo.Update
{
    public class UpdateManager
    {
        private readonly IReleasesClient _releasesClient;
        private readonly ILogger _logger;

        public UpdateManager(IReleasesClient releasesClient, ILogger<UpdateManager> logger)
        {
            this._releasesClient = releasesClient;
            this._logger = logger;
        }


        public Version NewVersion { get; private set; }

        public async Task<bool> CheckNewVersionAsync()
        {
            try
            {
                var lastVersion = await _releasesClient.GetLastVersionAsync();
                if (lastVersion == null)
                {
                    return false;
                }

                if (lastVersion > Global.GetVersion())
                {
                    NewVersion = lastVersion;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check available versions");

                return false;
            }
        }
    }
}
