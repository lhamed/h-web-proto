using System;
using System.Collections.Generic;
using HWebProto.Models;

namespace HWebProto.Services
{
    /// <summary>
    /// 런타임 게임 상태 — HP, Mental, 인벤토리, 장비, 플래그, 현재 이벤트 Key.
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

        // ── 능력치 ───────────────────────────────────────────────────────────
        public int Strength     { get; private set; } = 10;
        public int Dexterity    { get; private set; } = 10;
        public int Constitution { get; private set; } = 10;
        public int Intelligence { get; private set; } = 10;
        public int Wisdom       { get; private set; } = 10;
        public int Charisma     { get; private set; } = 10;
        public int FreePoints   { get; private set; } = 3;

        // ── 인벤토리 (아이템Key → 갯수) ──────────────────────────────────────
        public Dictionary<long, int> Inventory { get; private set; } = new();

        // ── 장비 (슬롯명 → 아이템Key) ─────────────────────────────────────────
        public Dictionary<string, long> Equipment { get; private set; } = new();

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
            var firstUnit = gds.Data?.GameUnits.Count > 0 ? gds.Data.GameUnits[0] : null;
            if (firstUnit != null)
            {
                MaxHp = firstUnit.MaxHP;
                Hp    = MaxHp;
                // 장비 초기화
                Equipment.Clear();
                foreach (var slot in new[] { "head","left","body","right","shoes","ring1","ring2" })
                {
                    long k = gds.GetEquippedKey(firstUnit, slot);
                    if (k > 0) Equipment[slot] = k;
                }
            }
            else
            {
                Hp = MaxHp = 6;
            }
            Mental = MaxMental = 10;
            Level  = 1; Exp = 0; FreePoints = 3;
            Inventory.Clear(); Flags.Clear();
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
                "Level"          => Level,
                _                => 0
            };
            return cond.Op switch
            {
                "Equal"              => actual == cond.CompareValue,
                "NotEqual"           => actual != cond.CompareValue,
                "GreaterThan"        => actual >  cond.CompareValue,
                "GreaterThanOrEqual" => actual >= cond.CompareValue,
                "LessThan"           => actual <  cond.CompareValue,
                "LessThanOrEqual"    => actual <= cond.CompareValue,
                _                    => true
            };
        }

        // ── 능력치 투자 ──────────────────────────────────────────────────────

        public bool InvestStat(string stat)
        {
            if (FreePoints <= 0) return false;
            switch (stat)
            {
                case "Strength":     Strength++;     break;
                case "Dexterity":    Dexterity++;    break;
                case "Constitution": Constitution++; MaxHp++; Hp = Math.Min(Hp + 1, MaxHp); break;
                case "Intelligence": Intelligence++; break;
                case "Wisdom":       Wisdom++;       break;
                case "Charisma":     Charisma++;     break;
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

        static int ApplyOp(int cur, int value, string op) => op switch
        {
            "Add"      => cur + value,
            "Subtract" => cur - value,
            "Set"      => value,
            "Multiply" => cur * value,
            _          => cur + value
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
