using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using HWebProto.Models;

namespace HWebProto.Services
{
    /// <summary>
    /// EXPORTED.bytes(UTF-8 JSON)를 파싱하고 Key 기반 조회를 제공합니다.
    /// </summary>
    public class GameDataService
    {
        static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ExportDataDto? Data { get; private set; }
        public bool IsLoaded => Data != null;
        public bool IsUserLoaded { get; private set; }
        public string LoadedFileName { get; private set; } = "";
        public bool IsMasterDataLoaded { get; private set; }
        public string MasterDataFileName { get; private set; } = "";

        // 로컬라이제이션 캐시: key → 텍스트
        readonly Dictionary<string, string> _locCache = new();
        JsonElement? _masterLocalization;
        public string Lang { get; set; } = "ko";

        public void Load(byte[] bytes, string fileName = "", bool userLoaded = false)
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Data = JsonSerializer.Deserialize<ExportDataDto>(json, _opts)
                   ?? throw new Exception("EXPORTED.bytes 파싱 실패");
            IsUserLoaded = userLoaded;
            LoadedFileName = fileName;
            BuildLocalizationCache();
        }

        public void LoadMasterData(byte[] bytes, string fileName = "M.bytes")
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            var root = JsonSerializer.Deserialize<JsonElement>(json, _opts);
            _masterLocalization = root;
            IsMasterDataLoaded = true;
            MasterDataFileName = fileName;
            BuildLocalizationCache();
        }

        void BuildLocalizationCache()
        {
            _locCache.Clear();

            if (Data?.Localization is JsonElement root)
                BuildLocalizationCache(root);

            if (_masterLocalization is JsonElement masterRoot)
                BuildLocalizationCache(masterRoot);
        }

        void BuildLocalizationCache(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                BuildLocalizationCacheFromRows(root);
                return;
            }

            if (root.ValueKind != JsonValueKind.Object) return;

            // MasterData 형식: { "SheetName": [ {...row...}, ... ] }
            foreach (var sheet in root.EnumerateObject())
            {
                if (sheet.Value.ValueKind != JsonValueKind.Array) continue;
                BuildLocalizationCacheFromRows(sheet.Value);
            }
        }

        void BuildLocalizationCacheFromRows(JsonElement rows)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                string? key = null;
                foreach (var prop in row.EnumerateObject())
                {
                    if (prop.Name.Equals("Key", StringComparison.OrdinalIgnoreCase))
                    {
                        key = prop.Value.GetString();
                        break;
                    }
                }
                if (string.IsNullOrEmpty(key)) continue;

                string? text = null;
                foreach (var prop in row.EnumerateObject())
                {
                    if (IsLanguageColumn(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        text = prop.Value.GetString();
                        if (!string.IsNullOrEmpty(text)) break;
                    }
                }
                if (text == null)
                {
                    foreach (var prop in row.EnumerateObject())
                    {
                        if (prop.Name.Equals("Key", StringComparison.OrdinalIgnoreCase)) continue;
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            text = prop.Value.GetString();
                            break;
                        }
                    }
                }
                if (text != null)
                    _locCache[key] = text;
            }
        }

        bool IsLanguageColumn(string column)
        {
            string lang = Lang.Trim();
            if (column.Equals(lang, StringComparison.OrdinalIgnoreCase)) return true;

            return lang.Equals("ko", StringComparison.OrdinalIgnoreCase) ||
                   lang.Equals("kr", StringComparison.OrdinalIgnoreCase) ||
                   lang.Equals("ko-KR", StringComparison.OrdinalIgnoreCase)
                ? column.Equals("Korean", StringComparison.OrdinalIgnoreCase) ||
                  column.Equals("ko", StringComparison.OrdinalIgnoreCase)
                : column.Equals("English", StringComparison.OrdinalIgnoreCase) ||
                  column.Equals("en", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// TextKey → 현지화 텍스트. 키가 없으면 키 자체를 반환합니다.
        /// </summary>
        public string L(string? key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            return _locCache.TryGetValue(key, out var v) ? v : key;
        }

        // ── Lookup helpers ───────────────────────────────────────────────────

        public GameEventDto?    GetEvent(long key)   => Data?.GameEvents   .FirstOrDefault(e => e.Key == key);
        public GameItemDto?     GetItem(long key)    => Data?.GameItems    .FirstOrDefault(i => i.Key == key);
        public GameUnitDto?     GetUnit(long key)    => Data?.GameUnits    .FirstOrDefault(u => u.Key == key);
        public MonsterDataDto?  GetMonster(long key) => Data?.MonsterDatas .FirstOrDefault(m => m.Key == key);
        public DiceDataDto?     GetDice(long key)    => Data?.DiceDatas    .FirstOrDefault(d => d.Key == key);
        public MonsterGroupDto? GetMonsterGroup(long key) => Data?.MonsterGroups.FirstOrDefault(g => g.Key == key);
        public BattleDataDto?   GetBattle(long key)  => Data?.BattleDatas  .FirstOrDefault(b => b.Key == key);

        public long FirstEventKey()
            => Data?.GameEvents?.OrderBy(e => e.Key).FirstOrDefault()?.Key ?? 0;

        public long FirstBattleKey()
            => Data?.BattleDatas?.OrderBy(b => b.Key).FirstOrDefault()?.Key ?? 0;

        // 슬롯 이름 → 아이템 키 조회 헬퍼
        public long GetEquippedKey(GameUnitDto unit, string slot) => slot switch
        {
            "head"  => unit.HeadItemKey,
            "left"  => unit.LeftHandItemKey,
            "body"  => unit.BodyItemKey,
            "right" => unit.RightHandItemKey,
            "shoes" => unit.ShoesItemKey,
            "ring1" => unit.Ring1ItemKey,
            "ring2" => unit.Ring2ItemKey,
            _ => 0L
        };

        public string EquipTypeToSlot(string equipmentType) => equipmentType switch
        {
            "Head"      => "head",
            "LeftHand"  => "left",
            "Body"      => "body",
            "RightHand" => "right",
            "Shoes"     => "shoes",
            "Ring1"     => "ring1",
            "Ring2"     => "ring2",
            _ => ""
        };
    }
}
