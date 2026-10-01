using System.Reflection;
using System.Text.Json.Serialization;

namespace TypeModeling.Testing.Closure;

/// <summary>solution 閉包に対する構造規約の検査</summary>
public static class SolutionClosureConformance
{
    /// <summary>閉じた判別共用体を識別する属性名</summary>
    private const string ClosedUnionAttributeMetadataName =
        "TypeModeling.Domain.ClosedUnionAttribute";

    /// <summary>repo project inventory の閉包所属規約への違反</summary>
    /// <param name="closure">ロード済みの solution 閉包</param>
    /// <param name="repoProjectPaths">repository 内に存在する全 project の相対パス</param>
    /// <param name="excludedProjects">閉包外に置く project の相対パスと除外根拠</param>
    /// <returns>相対パス順の違反一覧</returns>
    public static IReadOnlyList<string> InventoryViolations(
        LoadedSolutionClosure closure,
        IEnumerable<string> repoProjectPaths,
        IReadOnlyDictionary<string, string> excludedProjects)
    {
        var repo = repoProjectPaths.ToHashSet(StringComparer.Ordinal);
        var staticClosure = closure.ExpectedProjectPaths.ToHashSet(StringComparer.Ordinal);
        return repo
            .Except(staticClosure, StringComparer.Ordinal)
            .Except(excludedProjects.Keys, StringComparer.Ordinal)
            .Select(project =>
                $"{project}: repo project is neither in the static closure nor explicitly excluded")
            .Concat(excludedProjects.Keys
                .Except(repo, StringComparer.Ordinal)
                .Select(project => $"{project}: exclusion is stale because the repo project does not exist"))
            .Concat(excludedProjects.Keys
                .Intersect(staticClosure, StringComparer.Ordinal)
                .Select(project => $"{project}: exclusion overlaps the static closure"))
            .Concat(excludedProjects
                .Where(exclusion => string.IsNullOrWhiteSpace(exclusion.Value))
                .Select(exclusion => $"{exclusion.Key}: exclusion has no justification"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>repository が所有する project の相対パス列挙</summary>
    /// <param name="repoRoot">検査対象 repository の絶対パス</param>
    /// <returns>序数比較で整列した相対パス</returns>
    public static IReadOnlyList<string> RepoProjectPaths(string repoRoot) =>
        RepoProjectInventory.ProjectPaths(repoRoot);

    /// <summary>閉じた型階層の登録規約への違反</summary>
    /// <param name="closure">ロード済みの solution 閉包</param>
    /// <returns>表示文字列順の違反一覧</returns>
    public static IReadOnlyList<string> ClosedHierarchyViolations(LoadedSolutionClosure closure)
    {
        var jsonViolations = closure.ReflectionTypes
            .Where(type => type.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false) is not null)
            .SelectMany(baseType =>
            {
                var registered = baseType
                    .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                    .Select(attribute => attribute.DerivedType)
                    .ToHashSet();
                return ConcreteAssignableTypes(closure, baseType)
                    .Where(candidate => !registered.Contains(candidate))
                    .Select(candidate =>
                        $"base {Display(closure, baseType)} <- unregistered subtype {Display(closure, candidate)}");
            });
        var closedUnionViolations = closure.ReflectionTypes
            .Where(type => type.IsClass && HasAttribute(type, ClosedUnionAttributeMetadataName))
            .SelectMany(baseType => ConcreteAssignableTypes(closure, baseType)
                .Where(candidate =>
                    candidate.DeclaringType != baseType
                    || !candidate.IsSealed
                    || !IsRecord(candidate))
                .Select(candidate =>
                    $"base {Display(closure, baseType)} <- subtype {Display(closure, candidate)} " +
                    $"(directly-nested={candidate.DeclaringType == baseType}, " +
                    $"sealed={candidate.IsSealed}, record={IsRecord(candidate)})"));
        return jsonViolations
            .Concat(closedUnionViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>基底型へ代入可能な具象型の列挙</summary>
    /// <param name="closure">ロード済みの solution 閉包</param>
    /// <param name="baseType">代入先の基底型</param>
    /// <returns>閉包内の具象型集合</returns>
    private static IEnumerable<Type> ConcreteAssignableTypes(
        LoadedSolutionClosure closure,
        Type baseType) =>
        closure.ReflectionTypes.Where(candidate =>
            candidate != baseType
            && IsAssignableToDefinition(candidate, baseType)
            && !candidate.IsAbstract
            && !candidate.IsInterface);

    // generic union の派生の基底連鎖は開いた定義でなく型引数つきの構築形を指すため、
    // 定義へ落として照合しないと generic な閉じた階層の違反が沈黙する。
    /// <summary>generic 型定義を考慮した代入可能性の判定</summary>
    /// <param name="candidate">代入元の候補型</param>
    /// <param name="baseType">代入先の基底型</param>
    /// <returns>代入可能な場合に true</returns>
    private static bool IsAssignableToDefinition(Type candidate, Type baseType) =>
        baseType.IsInterface
            ? candidate.GetInterfaces().Any(contract => MatchesDefinition(contract, baseType))
            : BaseChain(candidate).Any(ancestor => MatchesDefinition(ancestor, baseType));

    /// <summary>継承元を object までたどる列挙</summary>
    /// <param name="type">走査開始型</param>
    /// <returns>直接基底型から始まる継承元集合</returns>
    private static IEnumerable<Type> BaseChain(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            yield return current;
    }

    /// <summary>構築済み generic 型を含む型定義の照合</summary>
    /// <param name="candidate">照合対象の候補型</param>
    /// <param name="baseType">期待する型定義</param>
    /// <returns>型定義が一致する場合に true</returns>
    private static bool MatchesDefinition(Type candidate, Type baseType) =>
        candidate == baseType ||
        (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == baseType);

    /// <summary>metadata 名による属性付与の判定</summary>
    /// <param name="type">検査対象型</param>
    /// <param name="attributeMetadataName">属性の metadata 名</param>
    /// <returns>属性が付与されている場合に true</returns>
    private static bool HasAttribute(Type type, string attributeMetadataName) =>
        type.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == attributeMetadataName);

    /// <summary>compiler 生成 clone method による record 判定</summary>
    /// <param name="type">検査対象型</param>
    /// <returns>record の場合に true</returns>
    private static bool IsRecord(Type type) =>
        type.GetMethod(
            "<Clone>$",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) is not null;

    /// <summary>違反元 project を含む型名の表示</summary>
    /// <param name="closure">ロード済みの solution 閉包</param>
    /// <param name="type">表示対象型</param>
    /// <returns>違反元を特定できる型名</returns>
    private static string Display(LoadedSolutionClosure closure, Type type)
    {
        var assemblyName = type.Assembly.GetName().Name
            ?? throw new InvalidOperationException($"{type.FullName ?? type.Name} has no assembly name");
        var projectPath = closure.Projects
            .Single(project => project.AssemblyName == assemblyName)
            .ProjectPath;
        return $"{type.FullName ?? type.Name} (assembly {assemblyName}, source project {projectPath})";
    }
}
