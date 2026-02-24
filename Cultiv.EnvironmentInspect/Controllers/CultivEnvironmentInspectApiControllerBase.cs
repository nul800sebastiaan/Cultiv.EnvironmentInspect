using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace Cultiv.EnvironmentInspect.Controllers
{
    [ApiController]
    [BackOfficeRoute("cultivenvironmentinspect/api/v{version:apiVersion}")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [MapToApi(Constants.ApiName)]
    public class CultivEnvironmentInspectApiControllerBase : ControllerBase
    {
    }
}
