using System.ComponentModel.DataAnnotations;

namespace Cultiv.EnvironmentInspect.Data;

internal class UserPreference
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string UserKey { get; set; }

    [Required]
    [MaxLength(250)]
    public required string SettingKey { get; set; }

    public bool IsStarred { get; set; }

    [MaxLength(2000)]
    public string? StringValue { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }
}
