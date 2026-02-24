using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Core.Security;

using Cultiv.EnvironmentInspect.Services;

namespace Cultiv.EnvironmentInspect.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Cultiv.EnvironmentInspect")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class CultivEnvironmentInspectApiController : CultivEnvironmentInspectApiControllerBase
    {
        private readonly IEnvironmentInspectService _environmentService;
        private readonly IUserPreferencesService _userPreferencesService;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

        public CultivEnvironmentInspectApiController(
            IEnvironmentInspectService environmentService,
            IUserPreferencesService userPreferencesService,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        {
            _environmentService = environmentService;
            _userPreferencesService = userPreferencesService;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        }

        [HttpGet("getenvironment")]
        [MapToApiVersion("1.0")]
        public async Task<EnvironmentInspectResponse> GetEnvironment()
        {
            return await _environmentService.GetEnvironmentDataAsync();
        }

        [HttpGet("preferences")]
        [MapToApiVersion("1.0")]
        public async Task<UserPreferencesDto> GetUserPreferences()
        {
            var userKey = GetCurrentUserKey();
            return await _userPreferencesService.GetUserPreferencesAsync(userKey);
        }

        [HttpPost("preferences/toggle-star")]
        [MapToApiVersion("1.0")]
        public async Task<IActionResult> ToggleStar([FromBody] ToggleStarRequest request)
        {
            try
            {
                var userKey = GetCurrentUserKey();
                await _userPreferencesService.ToggleStarAsync(userKey, request.SettingKey);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("preferences/starred-settings")]
        [MapToApiVersion("1.0")]
        public async Task<IActionResult> SetStarredSettings([FromBody] SetStarredSettingsRequest request)
        {
            var userKey = GetCurrentUserKey();
            await _userPreferencesService.SetStarredSettingsAsync(userKey, request.StarredKeys);
            return Ok();
        }

        [HttpPost("preferences/ui-settings")]
        [MapToApiVersion("1.0")]
        public async Task<IActionResult> SaveUISettings([FromBody] UISettingsDto uiSettings)
        {
            try
            {
                var userKey = GetCurrentUserKey();
                await _userPreferencesService.SaveUISettingsAsync(userKey, uiSettings);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private string GetCurrentUserKey()
        {
            var currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("No current user found");
            }
            return currentUser.Key.ToString();
        }
    }

    public class EnvironmentInspectResponse
    {
        public List<EnvironmentVariable> Variables { get; set; } = new();
        public bool AzureWebAppAdvancedCopy { get; set; }
    }

    public class ToggleStarRequest
    {
        public required string SettingKey { get; set; }
    }

    public class SetStarredSettingsRequest
    {
        public required List<string> StarredKeys { get; set; }
    }
}
