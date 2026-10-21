using System.Collections.Generic;
using UnityEngine;

/// 캐릭터 5종(기획서 9.1.1)과 낮·밤 액티브(9.1.2 · 9.1.3 · 9.2).
///
/// 낮 액티브는 밤 액티브의 짝으로 정해지므로(9.1.1) 그 짝 표는 `nightskill` 시트가
/// 가진다 — 캐릭터 시트에 낮 스킬을 또 적으면 두 곳이 어긋난다.
[CreateAssetMenu(menuName = "Blood & Beans/데이터 표/캐릭터", fileName = nameof(CharacterDataTable))]
public sealed class CharacterDataTable : DataTableAsset
{
    [System.Serializable]
    public struct CharacterRow
    {
        /// 픽 번호. 배열 인덱스가 되므로 0부터 빈 칸 없이 센다.
        public int Index;

        /// 외형 키. 모델·초상 표가 읽는다.
        public CharacterId VisualId;

        [Column("nightSkill")] public NightSkill Night;
    }

    [System.Serializable]
    public struct DaySkillRow
    {
        [Column("daySkill")] public DaySkill Skill;
        public float Cooldown;
    }

    [System.Serializable]
    public struct NightSkillRow
    {
        [Column("nightSkill")] public NightSkill Skill;
        public float Cooldown;

        /// 9.1.1: 이 밤 액티브와 짝이 되는 낮 액티브.
        public DaySkill PairedDay;
    }

    [SerializeField] List<CharacterRow> characters = new();
    [SerializeField] List<DaySkillRow> daySkills = new();
    [SerializeField] List<NightSkillRow> nightSkills = new();

    public IReadOnlyList<CharacterRow> Characters => characters;
    public IReadOnlyList<DaySkillRow> DaySkills => daySkills;
    public IReadOnlyList<NightSkillRow> NightSkills => nightSkills;

    // --- 규칙이 읽는 값 ---
    //
    // 행에서 펴낸 값이라 직렬화하지 않는다. **코드에 기본값을 두지 않는다** — 수치를
    // 고칠 곳은 엑셀 하나뿐이어야 한다.

    /// 배열 인덱스가 곧 픽 번호다 (기획서 9.1.1). 낮 액티브는 밤 액티브의 짝이라 여기 없다.
    [System.NonSerialized]
    public (CharacterId Id, NightSkill Night)[] CharacterTable =
        System.Array.Empty<(CharacterId, NightSkill)>();

    /// 낮 액티브 쿨타임 (9.1.3 표). 순서는 `DaySkill` 열거자와 같고 [0]은 None이다.
    [System.NonSerialized] public float[] DaySkillCooldown = System.Array.Empty<float>();

    /// 밤 액티브 쿨타임 (9.2 표). 순서는 `NightSkill` 열거자와 같고 [0]은 None이다.
    [System.NonSerialized] public float[] NightSkillCooldown = System.Array.Empty<float>();

    /// 9.1.1: 낮 액티브는 밤 액티브의 짝이다. 순서는 `NightSkill` 열거자와 같다.
    [System.NonSerialized] public DaySkill[] DaySkillOfNight = System.Array.Empty<DaySkill>();

    protected override string DefaultCategory => "캐릭터";
    public override string[] SheetSlots => new[] { "캐릭터", "낮 스킬", "밤 스킬" };

    public override void ReadSheet(int index, SheetTable sheet)
    {
        switch (index)
        {
            case 0: sheet.Fill(characters, "index"); break;
            case 1: sheet.Fill(daySkills, "daySkill"); break;
            case 2: sheet.Fill(nightSkills, "nightSkill"); break;
        }
    }

    /// 행을 열거자·픽 번호 순서 배열로 편다. 행이 없는 시트는 빈 배열로 남는다.
    public override void Rebuild()
    {
        if (characters.Count > 0)
        {
            var size = 0;
            foreach (var row in characters) if (row.Index + 1 > size) size = row.Index + 1;

            var rows = new (CharacterId Id, NightSkill Night)[size];
            foreach (var row in characters)
            {
                if (row.Index < 0 || row.Index >= size) continue;
                rows[row.Index] = (row.VisualId, row.Night);
            }
            CharacterTable = rows;
        }

        if (daySkills.Count > 0)
        {
            var size = 0;
            foreach (var row in daySkills) if ((int)row.Skill + 1 > size) size = (int)row.Skill + 1;

            var cooldown = new float[size];
            foreach (var row in daySkills)
            {
                var i = (int)row.Skill;
                if (i < 0 || i >= size) continue;
                cooldown[i] = row.Cooldown;
            }
            DaySkillCooldown = cooldown;
        }

        if (nightSkills.Count > 0)
        {
            var size = 0;
            foreach (var row in nightSkills) if ((int)row.Skill + 1 > size) size = (int)row.Skill + 1;

            var cooldown = new float[size];
            var paired = new DaySkill[size];
            foreach (var row in nightSkills)
            {
                var i = (int)row.Skill;
                if (i < 0 || i >= size) continue;
                cooldown[i] = row.Cooldown;
                paired[i] = row.PairedDay;
            }
            NightSkillCooldown = cooldown;
            DaySkillOfNight = paired;
        }
    }

    public override void WriteTo(BalanceData d, List<string> problems)
    {
        d.Characters = CharacterTable;
        d.DaySkillCooldown = DaySkillCooldown;
        d.NightSkillCooldown = NightSkillCooldown;
        d.DaySkillOfNight = DaySkillOfNight;
    }
}
