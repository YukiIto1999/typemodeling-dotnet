; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TYPMOD002: ValueObject の Parse 欠落と署名不適合を生成時の error として報告

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD002 | TypeModeling | Error | ValueObject の Parse 欠落と引数型、返却型、アクセシビリティを含む署名不適合の禁止
TYPMOD003 | TypeModeling | Error | abstract でない閉じた class の禁止
TYPMOD004 | TypeModeling | Error | 別の assembly の閉じた class からの直接の派生の禁止(C# 15 の CS9382 に対応)
TYPMOD005 | TypeModeling | Error | 閉じた基底型で参照しない型引数の禁止(C# 15 の CS9383 に対応)
TYPMOD006 | TypeModeling | Error | 閉じた型の網羅で到達できない arm の禁止(C# 15 の CS8510 に対応)
TYPMOD007 | TypeModeling | Error | struct でない union 宣言の形の禁止
TYPMOD008 | TypeModeling | Error | union 宣言の形の instance field、自動実装 property、field 形式の event の禁止(C# 15 の CS9373 に対応)
TYPMOD009 | TypeModeling | Error | union 宣言の形の partial でない引数一つの公開コンストラクタの禁止(C# 15 の CS9374 に対応)
TYPMOD010 | TypeModeling | Error | union 宣言の形の this 初期化子のないコンストラクタの禁止(C# 15 の CS9375 に対応)
TYPMOD011 | TypeModeling | Error | union 宣言の形の union member provider の禁止(C# 15 の CS9387 に対応)
TYPMOD012 | TypeModeling | Error | union 構築子のない union 型の禁止(C# 15 の CS9385 に対応)
TYPMOD013 | TypeModeling | Error | Value のない union 型の禁止(C# 15 の CS9386 に対応)
TYPMOD014 | TypeModeling | Error | partial でない型に入れ子の union 宣言の形の禁止
TYPMOD015 | TypeModeling | Error | default と引数なしの構築による未初期化の Result の禁止
TYPMOD016 | TypeModeling | Error | Result の Value の case 型への cast による取り出しの禁止
TYPMOD017 | TypeModeling | Warning | MustUse を付けた型と method の値を式文で捨てることの警告
TYPMOD018 | TypeModeling | Error | 0 に当たる member のない閉じた enum の禁止(C# 16 の closed enum に対応)
TYPMOD019 | TypeModeling | Error | 名前付きの member に当たらない値の閉じた enum への変換の禁止(C# 16 の closed enum に対応)
TYPMOD020 | TypeModeling | Error | 名前付きの member に当たらない値を作る閉じた enum の演算の禁止(C# 16 の closed enum に対応)

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD001 | TypeModeling | Warning | ClosedUnion、JsonPolymorphic、closed enum の variant を case pattern または when guard の is・constant・built-in equality/inequality で判別する switch statement の禁止。算術演算を含む式は対象外
