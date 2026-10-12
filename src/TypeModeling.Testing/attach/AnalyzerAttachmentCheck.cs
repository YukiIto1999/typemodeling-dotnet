namespace TypeModeling.Testing.Attach;

/// <summary>一回の走査で同時に判ずる analyzer attachment の一つの検め</summary>
/// <param name="Requirement">analyzer attachment 要件</param>
/// <param name="Detector">guarded feature の semantic 検出器</param>
public sealed record AnalyzerAttachmentCheck(
    AnalyzerAttachmentRequirement Requirement,
    IGuardedFeatureDetector Detector);
