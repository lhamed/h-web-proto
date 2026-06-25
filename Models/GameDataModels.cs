using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HWebProto.Models
{
    public class ExportDataDto
    {
        public int                    Version       { get; set; }
        public List<GameEventDto>     GameEvents    { get; set; } = new();
        public List<GameItemDto>      GameItems     { get; set; } = new();
        public List<GameUnitDto>      GameUnits     { get; set; } = new();
        public List<DiceDataDto>      DiceDatas     { get; set; } = new();
        public List<MonsterGroupDto>  MonsterGroups { get; set; } = new();
        public List<BattleDataDto>    BattleDatas   { get; set; } = new();
        public JsonElement?           Localization  { get; set; }
    }

    // ── GameEvent ──────────────────────────────────────────────────────────────

    public class GameEventDto
    {
        public long                    Key              { get; set; }
        public List<ConditionDto>      Conditions       { get; set; } = new();
        public List<ContentBlockDto>   ContentBlocks    { get; set; } = new();
        public string                  NextEventMode    { get; set; } = "First";
        public List<long>              NextEventKeys    { get; set; } = new();
        public List<NextEventEntryDto> NextEventEntries { get; set; } = new();
    }

    public class ContentBlockDto
    {
        public List<ContentItemDto> ContentItems  { get; set; } = new();
        public List<EffectDto>      CommonEffects { get; set; } = new();
        public List<SelectionDto>   Selections    { get; set; } = new();
    }

    public class ContentItemDto
    {
        public string Type           { get; set; } = "";
        public string TextKey        { get; set; } = "";
        public string SpeakerNameKey { get; set; } = "";
        public string ExpressionPath { get; set; } = "";
        public string ImagePath      { get; set; } = "";
    }

    public class SelectionDto
    {
        public string         SelectionTextKey      { get; set; } = "";
        public string         ResultTextKey         { get; set; } = "";
        public List<EffectDto> Effects              { get; set; } = new();
        public long           BattleVictoryEventKey { get; set; }
        public long           BattleDefeatEventKey  { get; set; }
    }

    public class ConditionDto
    {
        public string DataType     { get; set; } = "";
        public long   DataKey      { get; set; }
        public string Op           { get; set; } = "";
        public long   CompareValue { get; set; }
    }

    public class EffectDto
    {
        public string DataType { get; set; } = "";
        public long   DataKey  { get; set; }
        public string Op       { get; set; } = "";
        public long   Value    { get; set; }
    }

    public class NextEventEntryDto
    {
        public List<ConditionDto> Conditions { get; set; } = new();
        public long               EventKey   { get; set; }
    }

    // ── Item / Unit ────────────────────────────────────────────────────────────

    public class GameItemDto
    {
        public long   Key            { get; set; }
        public string NameKey        { get; set; } = "";
        public string DescriptionKey { get; set; } = "";
        public bool   Stackable      { get; set; }
        public bool   Usable         { get; set; }
        public bool   Equippable     { get; set; }
        public string EquipmentType  { get; set; } = "None";
    }

    public class GameUnitDto
    {
        public long   Key            { get; set; }
        public string NameKey        { get; set; } = "";
        public int    MaxHP          { get; set; } = 10;
        public long   AttackDiceKey  { get; set; }
        public bool   CanEquipItems  { get; set; } = true;
        public long   LeftHandItemKey  { get; set; }
        public long   RightHandItemKey { get; set; }
        public long   HeadItemKey      { get; set; }
        public long   BodyItemKey      { get; set; }
        public long   ShoesItemKey     { get; set; }
        public long   Ring1ItemKey     { get; set; }
        public long   Ring2ItemKey     { get; set; }
    }

    public class DiceDataDto
    {
        public long      Key         { get; set; }
        public int       Count       { get; set; } = 1;
        public int       Faces       { get; set; } = 6;
        public List<int> CustomFaces { get; set; } = new();
    }

    public class MonsterGroupDto
    {
        public long       Key        { get; set; }
        public List<long> MemberKeys { get; set; } = new();
    }

    public class BattleDataDto
    {
        public long Key             { get; set; }
        public long MonsterGroupKey { get; set; }
    }
}
