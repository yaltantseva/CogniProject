namespace Cogni.Database.Entities;

public class MbtiCompatibility
{
    public int Id { get; set; }
    public int SourceMbtiTypeId { get; set; }
    public int TargetMbtiTypeId { get; set; }
    public int CompatibilityPercentage { get; set; }

    public virtual MbtiType SourceMbtiType { get; set; } = null!;
    public virtual MbtiType TargetMbtiType { get; set; } = null!;
}