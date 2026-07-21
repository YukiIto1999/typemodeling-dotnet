; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD001 | TypeModeling | Warning | ClosedUnion、JsonPolymorphic、closed enum の variant を case pattern または when guard の is・constant・built-in equality/inequality で判別する switch statement の禁止。算術演算を含む式は対象外
