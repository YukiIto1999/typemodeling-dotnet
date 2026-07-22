using Microsoft.CodeAnalysis;

namespace TypeModeling.Testing.Attach;

/// <summary>semantic compilation に対する guarded feature 利用判定</summary>
public interface IGuardedFeatureDetector
{
    /// <summary>guarded feature の利用判定</summary>
    /// <param name="compilation">検査対象 compilation</param>
    /// <returns>guarded feature を使う場合に true</returns>
    bool UsesGuardedFeature(Compilation compilation);
}
