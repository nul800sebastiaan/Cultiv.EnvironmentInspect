using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Umbraco.Cms.Web.Common.Authorization;

using Cultiv.EnvironmentInspect.Services;

namespace Cultiv.EnvironmentInspect.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Cultiv.EnvironmentInspect")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class CultivEnvironmentInspectApiController : CultivEnvironmentInspectApiControllerBase
    {
        private readonly IEnvironmentInspectService _environmentService;

        public CultivEnvironmentInspectApiController(IEnvironmentInspectService environmentService)
        {
            _environmentService = environmentService;
        }

        [HttpGet("getenvironment")]
        [MapToApiVersion("1.0")]
        public async Task<EnvironmentInspectResponse> GetEnvironment()
        {
            return await _environmentService.GetEnvironmentDataAsync();
        }
    }

    public class EnvironmentInspectResponse
    {
        public List<EnvironmentVariable> Variables { get; set; } = new();
        public bool AzureWebAppAdvancedCopy { get; set; }
    }
}
