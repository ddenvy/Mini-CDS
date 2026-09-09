// c:\Develop\Mini-CDS\src\MiniCds.Domain\Entities\Report.cs
namespace MiniCds.Domain.Entities;

public sealed class Report
{
    public long Id { get; set; }
    public required string Title { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public long GeneratedByUserId { get; set; }
    public User? GeneratedBy { get; set; }
    public required string FilePath { get; set; }
    public required string Format { get; set; }
}