; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TYPMOD001: switch case pattern または when guard の is・constant・built-in equality/inequality が ClosedUnion、JsonPolymorphic、closed enum の variant を判別する局所形を governing value の origin に依存せず禁止。case 値と guard operand は value-preserving reduction のみを辿る
; Early warning TYPMOD001: 同一 executable body の local origin 追跡を補完として維持
; TYPMOD002: ValueObject の Parse 欠落と署名不適合を生成時の error として報告

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD002 | TypeModeling | Error | ValueObject の Parse 欠落と引数型、返却型、アクセシビリティを含む署名不適合の禁止
