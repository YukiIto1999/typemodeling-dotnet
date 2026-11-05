; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TYPMOD002: ValueObject の Parse 欠落と署名不適合を生成時の error として報告

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD002 | TypeModeling | Error | ValueObject の Parse 欠落と引数型、返却型、アクセシビリティを含む署名不適合の禁止

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD001 | TypeModeling | Warning | ClosedUnion、JsonPolymorphic、closed enum の variant を case pattern または when guard の is・constant・built-in equality/inequality で判別する switch statement の禁止。算術演算を含む式は対象外
