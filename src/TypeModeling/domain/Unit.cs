namespace TypeModeling.Domain;

/// <summary>値を持たない成功を表す唯一値の型</summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>唯一の <see cref="Unit"/> の値</summary>
    public static Unit Value => default;

    /// <summary>常に等しいことを表す <see cref="Unit"/> どうしの等価判定</summary>
    /// <param name="other">比較する相手</param>
    /// <returns>常に <see langword="true"/></returns>
    public bool Equals(Unit other) => true;

    /// <summary>相手が <see cref="Unit"/> であることだけを見る等価判定</summary>
    /// <param name="obj">比較する相手</param>
    /// <returns>相手が <see cref="Unit"/> のときの <see langword="true"/></returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>唯一値ゆえ常に同一のハッシュ値</summary>
    /// <returns>常に <c>0</c></returns>
    public override int GetHashCode() => 0;

    /// <summary>唯一値の文字列表現</summary>
    /// <returns>常に <c>"()"</c></returns>
    public override string ToString() => "()";

    /// <summary>常に等しいことを表す等価演算</summary>
    /// <param name="left">左辺</param>
    /// <param name="right">右辺</param>
    /// <returns>常に <see langword="true"/></returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>常に等しくないとはならないことを表す非等価演算</summary>
    /// <param name="left">左辺</param>
    /// <param name="right">右辺</param>
    /// <returns>常に <see langword="false"/></returns>
    public static bool operator !=(Unit left, Unit right) => false;
}
