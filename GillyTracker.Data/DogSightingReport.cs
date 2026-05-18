using System.ComponentModel.DataAnnotations;

namespace GillyTracker.Data;

public class DogSightingReport
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public decimal Latitude { get; set; }

    [Required]
    public decimal Longitude { get; set; }

    [MaxLength(2000)]
    public string? ReporterDetails { get; set; }

    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
}
