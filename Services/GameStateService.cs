using System;
using System.Collections.Generic;
using HWebProto.Models;

namespace HWebProto.Services
{
    /// <summary>
    /// 런타임 게임 상태 — HP, Mental, 플레이어 4스탯, 인벤토리, 장비, 플래그, 현재 이벤트 Key.
    /// </summary>
    public class GameStateService
    {
        // ── 바이탈 ───────────────────────────────────────────────────────────
        public int Hp        { get; private set; } = 5;
        public int MaxHp     { get; private set; } = 6;
        public int Mental    { get; private set; } = 8;
        public int MaxMental { get; private set; } = 10;
        public int Level     { get; private set; } = 1;
        public int Exp       { get; private set; } = 0;
        public int NextExp   { get; private set; } = 100;
        public bool IsGameOver => Hp <= 0;

        // ── 플레이어 스탯 (Unity PlayerStatType: 0 정신력, 1 체력, 2 완력, 3 민첩성) ───
        public int MentalStat { get; private set; } = 1;
        public int Vitality   { get; private set; } = 1;
        public int Strength   { get; private set; } = 1;
        public int Dexterity  { get; private set; } = 1;
        public int FreePoints   { get; private set; } = 3;

        // ── 인벤토리 (아이템Key → 갯수) ──────────────────────────────────────
        public Dictionary<long, int> Inventory { get; private set; } = new();

        // ── 장비 (슬롯명 → 아이템Key) ─────────────────────────────────────────
        public Dictionary<string, long> Equipment { get; private set; } = new();

        // ── 파티 (GameUnit Key 목록) ─────────────────────────────────────────
        public List<long> PartyKeys { get; private set; } = new();

        // ── 플래그 (flagKey → 값) ─────────────────────────────────────────────
        public Dictionary<long, long> Flags { get; private set; } = new();

        // ── 현재 이벤트 ──────────────────────────────────────────────────────
        public long CurrentEventKey { get; set; }
        public long PendingBattleKey { get; set; }
        public long VictoryEventKey  { get; set; }
        public long DefeatEventKey   { get; set; }

        public event Action? OnStateChanged;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>
        /// EXPORTED.bytes 로드 후, 첫 번째 플레이어 유닛으로 상태를 초기화합니다.
        /// </summary>
        public void InitFromData(GameDataService gds)
        {
            var playerUnit = gds.GetUnit(1) ?? gds.Data?.GameUnits.Find(u => u.CanEquipItems);
            if (playerUnit != null)
            {
                MaxHp = playerUnit.MaxHP;
                Hp    = MaxHp;
                MentalStat = Math.Max(1, playerUnit.Mental);
                Vitality   = Math.Max(1, playerUnit.Vitality);
                Strength   = Math.Max(1, playerUnit.Strength);
                Dexterity  = Math.Max(1, playerUnit.Dexterity);
                // 장비 초기화
                Equipment.Clear();
                foreach (var slot in new[] { "head","left","body","right","shoes","ring1","ring2" })
                {
                    long k = gds.GetEquippedKey(playerUnit, slot);
                    if (k > 0) Equipment[slot] = k;
                }
            }
            else
            {
                Hp = MaxHp = 6;
                MentalStat = Vitality = Strength = Dexterity = 1;
            }
            Mental = MaxMental = 10;
            Level  = 1; Exp = 0; FreePoints = 3;
            Inventory.Clear(); Flags.Clear();
            PartyKeys.Clear();
            if (playerUnit != null) PartyKeys.Add(playerUnit.Key);
            CurrentEventKey = gds.FirstEventKey();
            OnStateChanged?.Invoke();
        }

        // ── Effect 적용 ───────────────────────────────────────────────────────

        public void ApplyEffect(EffectDto effect)
        {
            switch (effect.DataType)
            {
                case "Hp":
                case "HP":
                    ModifyHp((int)effect.Value, effect.Op);
                    break;
                case "Mental":
                    ModifyMental((int)effect.Value, effect.Op);
                    break;
                case "Item":
                    ModifyItem(effect.DataKey, (int)effect.Value, effect.Op);
                    break;
                case "Flag":
                    ModifyFlag(effect.DataKey, effect.Value, effect.Op);
                    break;
                case "Stat":
                    ModifyStat(effect.DataKey, effect.Value, effect.Op);
                    break;
                case "Party":
                    ModifyParty(effect.DataKey, effect.Op);
                    break;
                case "Exp":
                    Exp = ApplyOp(Exp, (int)effect.Value, effect.Op);
                    TryLevelUp();
                    break;
            }
            OnStateChanged?.Invoke();
        }

        public bool CheckCondition(ConditionDto cond)
        {
            long actual = cond.DataType switch
            {
                "Hp"     or "HP" => Hp,
                "Mental"         => Mental,
                "Item"           => Inventory.TryGetValue(cond.DataKey, out var cnt) ? cnt : 0,
                "Flag"           => Flags.TryGetValue(cond.DataKey, out var f)       ? f   : 0,
                "Stat"           => GetStat(cond.DataKey),
                "Party"          => PartyKeys.Contains(cond.DataKey) ? 1 : 0,
                "Level"          => Level,
                _                => 0
            };
            return cond.Op switch
            {
                "Equal"                          => actual == cond.CompareValue,
                "NotEqual"                       => actual != cond.CompareValue,
                "Greater"     or "GreaterThan"        => actual >  cond.CompareValue,
                "GreaterEqual" or "GreaterThanOrEqual" => actual >= cond.CompareValue,
                "Less"        or "LessThan"           => actual <  cond.CompareValue,
                "LessEqual"   or "LessThanOrEqual"    => actual <= cond.CompareValue,
                _                                => true
            };
        }

        // ── 능력치 투자 ──────────────────────────────────────────────────────

        public bool InvestStat(string stat)
        {
            if (FreePoints <= 0) return false;
            switch (stat)
            {
                case "Mental":   MentalStat++; break;
                case "Vitality": Vitality++; break;
                case "Strength": Strength++; break;
                case "Dexterity": Dexterity++; break;
                default: return false;
            }
            FreePoints--;
            OnStateChanged?.Invoke();
            return true;
        }

        // ── 아이템 사용/장착 ─────────────────────────────────────────────────

        public bool UseItem(long itemKey)
        {
            if (!Inventory.TryGetValue(itemKey, out int cnt) || cnt <= 0) return false;
            Inventory[itemKey] = cnt - 1;
            if (Inventory[itemKey] <= 0) Inventory.Remove(itemKey);
            OnStateChanged?.Invoke();
            return true;
        }

        public void EquipItem(string slot, long itemKey)
        {
            if (itemKey <= 0) Equipment.Remove(slot);
            else Equipment[slot] = itemKey;
            OnStateChanged?.Invoke();
        }

        public void UnequipSlot(string slot)
        {
            Equipment.Remove(slot);
            OnStateChanged?.Invoke();
        }

        public void AddItem(long itemKey, int count = 1)
        {
            Inventory.TryGetValue(itemKey, out var cur);
            Inventory[itemKey] = cur + count;
            OnStateChanged?.Invoke();
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────────

        void ModifyHp(int value, string op)
        {
            Hp = Math.Clamp(ApplyOp(Hp, value, op), 0, MaxHp);
        }

        void ModifyMental(int value, string op)
        {
            Mental = Math.Clamp(ApplyOp(Mental, value, op), 0, MaxMental);
        }

        void ModifyItem(long key, int value, string op)
        {
            Inventory.TryGetValue(key, out var cur);
            int next = Math.Max(0, ApplyOp(cur, value, op));
            if (next <= 0) Inventory.Remove(key);
            else Inventory[key] = next;
        }

        void ModifyFlag(long key, long value, string op)
        {
            Flags.TryGetValue(key, out var cur);
            Flags[key] = ApplyOp((int)cur, (int)value, op);
        }

        void ModifyParty(long key, string op)
        {
            switch (op)
            {
                case "Subtract":
                    PartyKeys.Remove(key);
                    break;
                case "Set":
                case "Assign":
                    PartyKeys.Clear();
                    if (key > 0) PartyKeys.Add(key);
                    break;
                default:
                    if (key > 0 && !PartyKeys.Contains(key))
                        PartyKeys.Add(key);
                    break;
            }
        }

        void ModifyStat(long key, long value, string op)
        {
            long current = GetStat(key);
            long next = op switch
            {
                "Add" => current + value,
                "Subtract" => current - value,
                "Set" or "Assign" => value,
                _ => current + value
            };

            SetStat(key, ClampToInt(next));
        }

        long GetStat(long key) => key switch
        {
            0 => MentalStat,
            1 => Vitality,
            2 => Strength,
            3 => Dexterity,
            _ => 0
        };

        void SetStat(long key, int value)
        {
            value = Math.Max(1, value);
            switch (key)
            {
                case 0: MentalStat = value; break;
                case 1: Vitality = value; break;
                case 2: Strength = value; break;
                case 3: Dexterity = value; break;
            }
        }

        static int ClampToInt(long value)
        {
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }

        static int ApplyOp(int cur, int value, string op) => op switch
        {
            "Add"             => cur + value,
            "Subtract"        => cur - value,
            "Set" or "Assign" => value,
            "Multiply"        => cur * value,
            _                 => cur + value
        };

        void TryLevelUp()
        {
            while (Exp >= NextExp)
            {
                Exp     -= NextExp;
                Level++;
                NextExp  = Level * 100;
                FreePoints += 2;
            }
        }
    }
}
