using System.Diagnostics;
using UnityEngine;

/// 릴리스 빌드에서 사라지는 로그. `[Conditional]`은 호출부 자체를 제거하므로
/// 인자로 넘긴 문자열 결합·보간 비용도 함께 사라진다.
/// 에러는 릴리스에서도 남겨야 하므로 조건부가 아니다.
public static class CDebug
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message, Object context = null)
        => UnityEngine.Debug.Log(message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message, Object context = null)
        => UnityEngine.Debug.LogWarning(message, context);

    public static void LogError(object message, Object context = null)
        => UnityEngine.Debug.LogError(message, context);
}
