using System;

/// <summary>
/// UnityCommunity/UnitySingleton 기반의 순수 C# 제네릭 싱글톤
/// MonoBehaviour를 상속받지 않는 일반 클래스용입니다.
/// </summary>
public abstract class Singleton<T> where T : class, new()
{
    private static readonly Lazy<T> instance = new Lazy<T>(() => new T());

    public static T Instance => instance.Value;

    protected Singleton()
    {
        if (instance.IsValueCreated)
        {
            throw new InvalidOperationException("이 싱글톤 인스턴스는 이미 생성되었습니다.");
        }
    }
}
