using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// 아틀라스 속 스프라이트를 가리키는 약한 참조. 아틀라스 GUID와 스프라이트 이름만 직렬화하므로
/// 이 필드를 가진 프리팹을 로드해도 아틀라스가 딸려 올라오지 않는다.
/// 불러오기는 <c>ResourceManager.Instance.LoadAsync&lt;Sprite&gt;(ref)</c>, 해제는 같은 키로 <c>Release&lt;Sprite&gt;</c>.
/// Inspector에서는 스프라이트를 끌어다 놓는다 (`AtlasSpriteRefDrawer`). 아틀라스는 Addressables 그룹에 있어야 한다.
[Serializable]
public class AtlasSpriteRef : IKeyEvaluator, ISerializationCallbackReceiver
{
    [SerializeField] string atlasGuid;
    [SerializeField] string spriteName;

    // Addressables의 서브 오브젝트 키 형식 `guid[이름]`. 부를 때마다 문자열을 만들지 않게 한 번만 만든다.
    [NonSerialized] string key;

    public object RuntimeKey => key ??= $"{atlasGuid}[{spriteName}]";

    public bool RuntimeKeyIsValid() => !string.IsNullOrEmpty(atlasGuid) && !string.IsNullOrEmpty(spriteName);

    public override string ToString() => RuntimeKeyIsValid() ? spriteName : "(비어 있음)";

    // Inspector에서 값이 바뀌면 다시 역직렬화된다. 그때 만들어 둔 키를 버린다.
    void ISerializationCallbackReceiver.OnAfterDeserialize() => key = null;
    void ISerializationCallbackReceiver.OnBeforeSerialize() { }
}
