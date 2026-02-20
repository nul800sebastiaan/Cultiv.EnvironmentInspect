using System.Threading.Tasks;
using Cultiv.EnvironmentInspect.Controllers;

namespace Cultiv.EnvironmentInspect.Services
{
    public interface IEnvironmentInspectService
    {
        Task<EnvironmentInspectResponse> GetEnvironmentDataAsync();
    }

    public class EnvironmentVariable
    {
        public string Key { get; set; }
        public string? Value { get; set; }
        public string? Provider { get; set; }
        public string? ProviderType { get; set; }
        public string? ProviderSource { get; set; }
        public string? RedactedMode { get; set; }
    }
}
