using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// UI 스프라이트 조회. 표의 각 칸은 <see cref="AtlasSpriteRef"/>라서 애셋에는 아틀라스 GUID와 이름만 남는다.
/// 이 애셋을 읽어도 스프라이트는 <see cref="PreloadSpritesAsync"/> 전까지 올라오지 않는다.
public partial class ResourceManager
{
    /// 이름으로 꺼내는 스프라이트의 id. `namedSprites` 표의 id와 같아야 한다.
    public const string BagClosed = "BagIcon";
    public const string BagOpen = "BagIcon_Open";

    [Serializable]
    public struct NamedSpriteEntry
    {
        public string id;
        public AtlasSpriteRef sprite;
    }

    [Serializable]
    public struct IngredientSpriteEntry
    {
        public Ingredient id;
        public AtlasSpriteRef sprite;
    }

    [Serializable]
    public struct MenuSpriteEntry
    {
        public MenuId id;
        public AtlasSpriteRef sprite;
    }

    /// 키가 `CharacterId`인 이유는 `CharacterVisualConfig`와 같다.
    [Serializable]
    public struct CrewSpriteEntry
    {
        public CharacterId id;
        public AtlasSpriteRef sprite;
    }

    [Header("스프라이트")]
    [SerializeField] NamedSpriteEntry[] namedSprites = Array.Empty<NamedSpriteEntry>();
    [Tooltip("재료·보석 아이콘.")]
    [SerializeField] IngredientSpriteEntry[] ingredientSprites = Array.Empty<IngredientSpriteEntry>();
    [Tooltip("완성품 아이콘. 주문과 조합법이 함께 쓴다.")]
    [SerializeField] MenuSpriteEntry[] menuSprites = Array.Empty<MenuSpriteEntry>();
    [Tooltip("캐릭터 초상.")]
    [SerializeField] CrewSpriteEntry[] crewSprites = Array.Empty<CrewSpriteEntry>();

    /// 흐름이 불러 두고 놓는 표 단위. 표마다 쓰는 아틀라스가 달라서 이 단위로 메모리가 갈린다.
    [Flags]
    public enum SpriteTables
    {
        None = 0,
        Named = 1,
        Ingredients = 2,
        Crews = 4,
        Menus = 8,
        All = Named | Ingredients | Crews | Menus,
    }

    // 표 하나의 상태. 쓰는 곳이 몇인지 세고, 0이 되면 스프라이트를 놓는다.
    sealed class SpriteTable<TKey>
    {
        public Dictionary<TKey, Sprite> Sprites;   // 다 불렀을 때만 채운다. null이면 아직 없다
        public int Users;
        public AsyncLazy Loading;   // 끝나기 전에 여러 흐름이 기다릴 수 있어야 해서 AsyncLazy다
    }

    [NonSerialized] SpriteTable<string> sprites = new();
    [NonSerialized] SpriteTable<Ingredient> ingredients = new();
    [NonSerialized] SpriteTable<CharacterId> crews = new();
    [NonSerialized] SpriteTable<MenuId> menus = new();

    /// 표들을 비동기로 불러 둔다. 화면을 열기 전에 기다리고, 흐름을 떠날 때 같은 표로 <see cref="ReleaseSprites"/>를 부른다.
    /// 호출자가 취소해도 공유 로드는 계속되며, 부른 횟수는 이미 셌으므로 해제도 해야 한다.
    public UniTask PreloadSpritesAsync(SpriteTables tables, CancellationToken ct = default)
    {
        var loads = new List<UniTask>(4);
        if ((tables & SpriteTables.Named) != 0) loads.Add(Acquire(sprites, NamedEntries()));
        if ((tables & SpriteTables.Ingredients) != 0) loads.Add(Acquire(ingredients, IngredientEntries()));
        if ((tables & SpriteTables.Crews) != 0) loads.Add(Acquire(crews, CrewEntries()));
        if ((tables & SpriteTables.Menus) != 0) loads.Add(Acquire(menus, MenuEntries()));
        return UniTask.WhenAll(loads).AttachExternalCancellation(ct);
    }

    public void ReleaseSprites(SpriteTables tables)
    {
        if ((tables & SpriteTables.Named) != 0) ReleaseTable(sprites, NamedEntries());
        if ((tables & SpriteTables.Ingredients) != 0) ReleaseTable(ingredients, IngredientEntries());
        if ((tables & SpriteTables.Crews) != 0) ReleaseTable(crews, CrewEntries());
        if ((tables & SpriteTables.Menus) != 0) ReleaseTable(menus, MenuEntries());
    }

    /// id로 찾는다. 없으면 오류를 남기고 null이다.
    public Sprite GetSprite(string id)
    {
        var sprite = Lookup(sprites, id);
        if (sprite == null && sprites.Sprites != null)
            CDebug.LogError($"{AssetName}: '{id}' 스프라이트가 {nameof(namedSprites)} 표에 없다.", this);
        return sprite;
    }

    /// 재료·보석 아이콘. 표에 없으면 null이다.
    public Sprite IngredientSprite(Ingredient item) => Lookup(ingredients, item);

    /// 완성품 아이콘. 주문과 조합법이 같은 표를 읽는다.
    public Sprite MenuSprite(MenuId id) => Lookup(menus, id);

    /// 캐릭터 초상. 표에 없으면 null이다.
    public Sprite CrewSprite(CharacterId id) => Lookup(crews, id);

    Sprite Lookup<TKey>(SpriteTable<TKey> table, TKey id)
    {
#if UNITY_EDITOR
        // 재생 전 에디터 도구는 기다릴 프레임이 없어 막고 불러온다. 빌드에는 들어가지 않는다.
        if (table.Sprites == null && !Application.isPlaying) LoadAllBlockingForEditor();
#endif
        if (table.Sprites == null)
        {
            CDebug.LogError($"{AssetName}: '{id}'이 든 표를 {nameof(PreloadSpritesAsync)}로 불러 두기 전에 찾았다.", this);
            return null;
        }
        return table.Sprites.TryGetValue(id, out var sprite) ? sprite : null;
    }

    IEnumerable<(string, AtlasSpriteRef)> NamedEntries() { foreach (var e in namedSprites) yield return (e.id, e.sprite); }
    IEnumerable<(Ingredient, AtlasSpriteRef)> IngredientEntries() { foreach (var e in ingredientSprites) yield return (e.id, e.sprite); }
    IEnumerable<(MenuId, AtlasSpriteRef)> MenuEntries() { foreach (var e in menuSprites) yield return (e.id, e.sprite); }
    IEnumerable<(CharacterId, AtlasSpriteRef)> CrewEntries() { foreach (var e in crewSprites) yield return (e.id, e.sprite); }

    UniTask Acquire<TKey>(SpriteTable<TKey> table, IEnumerable<(TKey, AtlasSpriteRef)> entries)
    {
        table.Users++;
        if (table.Sprites != null) return UniTask.CompletedTask;
        if (table.Loading != null) return table.Loading.Task;
        var loading = LoadTableAsync(table, entries).ToAsyncLazy();
        // 즉시 끝났으면 끝난 자리가 이미 표를 채웠다. 남겨 두면 다음 번 불러오기를 건너뛴다.
        if (loading.Task.Status == UniTaskStatus.Pending) table.Loading = loading;
        return loading.Task;
    }

    async UniTask LoadTableAsync<TKey>(SpriteTable<TKey> table, IEnumerable<(TKey, AtlasSpriteRef)> entries)
    {
        var loaded = new Dictionary<TKey, Sprite>();
        var loads = new List<UniTask>();
        foreach (var (id, reference) in entries) loads.Add(LoadEntryAsync(reference, id, loaded));
        await UniTask.WhenAll(loads);
        table.Loading = null;

        // 다 채운 뒤에 공개한다. 기다리는 사이 모두 놓았으면 바로 돌려준다.
        table.Sprites = loaded;
        if (table.Users == 0) ReleaseLoaded(table, entries);
    }

    void ReleaseTable<TKey>(SpriteTable<TKey> table, IEnumerable<(TKey, AtlasSpriteRef)> entries)
    {
        if (table.Users == 0)
        {
            CDebug.LogError($"{AssetName}: 불러오지 않은 스프라이트 표를 놓으려 했다. 해제가 중복됐다.", this);
            return;
        }
        // 아직 불러오는 중이면 끝나는 자리(LoadTableAsync)가 돌려준다.
        if (--table.Users == 0 && table.Sprites != null) ReleaseLoaded(table, entries);
    }

    void ReleaseLoaded<TKey>(SpriteTable<TKey> table, IEnumerable<(TKey, AtlasSpriteRef)> entries)
    {
        foreach (var (id, reference) in entries)
            if (table.Sprites.Remove(id)) Release<Sprite>(reference);
        table.Sprites = null;
    }

    // 칸 하나가 비었거나 끊겨도 나머지 화면은 떠야 한다. 그 칸만 null로 두고 크게 알린다.
    async UniTask LoadEntryAsync<TKey>(AtlasSpriteRef reference, TKey id, Dictionary<TKey, Sprite> table)
    {
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            CDebug.LogError($"{AssetName}: '{id}' 칸에 스프라이트가 지정되지 않았다.", this);
            return;
        }
        if (table.ContainsKey(id))
        {
            CDebug.LogError($"{AssetName}: '{id}' 칸이 표에 둘이다. 앞의 것만 쓴다.", this);
            return;
        }

        try
        {
            table[id] = await LoadAsync<Sprite>(reference);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            CDebug.LogError($"{AssetName}: '{id}' 스프라이트({reference.RuntimeKey})를 불러오지 못했다. {e.Message}", this);
        }
    }

#if UNITY_EDITOR
    void LoadAllBlockingForEditor()
    {
        FillBlocking(sprites, NamedEntries());
        FillBlocking(ingredients, IngredientEntries());
        FillBlocking(crews, CrewEntries());
        FillBlocking(menus, MenuEntries());
    }

    void FillBlocking<TKey>(SpriteTable<TKey> table, IEnumerable<(TKey, AtlasSpriteRef)> entries)
    {
        if (table.Sprites != null) return;
        table.Sprites = new Dictionary<TKey, Sprite>();
        foreach (var (id, reference) in entries)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) continue;
            var sprite = LoadBlocking<Sprite>(reference);
            if (sprite == null) CDebug.LogError($"{AssetName}: '{id}' 스프라이트({reference.RuntimeKey})를 불러오지 못했다.", this);
            else table.Sprites[id] = sprite;
        }
    }
#endif
}
