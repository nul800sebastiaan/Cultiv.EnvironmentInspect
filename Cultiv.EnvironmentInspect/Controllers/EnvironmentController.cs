using System.Collections.Generic;
using System.Threading.Tasks;
using Cultiv.EnvironmentInspect.Services;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Authorization;

namespace Cultiv.EnvironmentInspect.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class EnvironmentController : UmbracoAuthorizedJsonController
    {
        private readonly IEnvironmentInspectService _environmentInspectService;

        public EnvironmentController(IEnvironmentInspectService environmentInspectService)
        {
            _environmentInspectService = environmentInspectService;
        }
        
        public async Task<EnvironmentInspectResponse> GetEnvironment()
        {
            return await _environmentInspectService.GetEnvironmentDataAsync();
        }
    }

    public class EnvironmentInspectResponse
    {
        public List<EnvironmentVariable> Variables { get; set; } = new();
        public bool AzureWebAppAdvancedCopy { get; set; }
    }
}