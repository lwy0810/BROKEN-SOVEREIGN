using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class StatSheetTests
    {
        private class TestSource : IStatModifierSource
        {
            public readonly List<StatModifier> Modifiers = new List<StatModifier>();

            public IEnumerable<StatModifier> GetModifiers() => Modifiers;
        }

        private const float TOLERANCE = 0.0001f;

        [Test]
        public void Get_FlatAndPercent_AddsFlatThenMultipliesPercent()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.AttackPower, 100f);
            TestSource source = new TestSource();
            source.Modifiers.Add(new StatModifier(StatType.AttackPower, ModifierType.Flat, 20f));
            source.Modifiers.Add(new StatModifier(StatType.AttackPower, ModifierType.Percent, 0.5f));
            sheet.AddSource(source);

            // (100 + 20) × 1.5 = 180
            Assert.AreEqual(180f, sheet.Get(StatType.AttackPower), TOLERANCE);
        }

        [Test]
        public void Get_DamageReduction_StacksMultiplicatively()
        {
            StatSheet sheet = new StatSheet();
            TestSource source = new TestSource();
            source.Modifiers.Add(new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, 0.3f));
            source.Modifiers.Add(new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, 0.3f));
            sheet.AddSource(source);

            // 1 − 0.7 × 0.7 = 0.51
            Assert.AreEqual(0.51f, sheet.Get(StatType.DamageReduction), TOLERANCE);
        }

        [Test]
        public void Get_Armor_IncludesStrength()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.Strength, 10f);
            sheet.SetBase(StatType.Armor, 5f);

            Assert.AreEqual(15f, sheet.Get(StatType.Armor), TOLERANCE);
        }

        [Test]
        public void Get_AttackSpeed_ConvergesFromAgility()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.Agility, 1000f);

            // 50% × 1000 ÷ (1000 + 1000) = 25%
            Assert.AreEqual(0.25f, sheet.Get(StatType.AttackSpeed), TOLERANCE);
        }

        [Test]
        public void Get_ElementalResistance_CappedAt70Percent()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.ElementalResistance, 0.9f);

            Assert.AreEqual(0.7f, sheet.Get(StatType.ElementalResistance), TOLERANCE);
        }

        [Test]
        public void Get_ElementalResistance_MaxResistanceRaisesCapUpTo85Percent()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.ElementalResistance, 0.9f);
            sheet.SetBase(StatType.MaxElementalResistance, 0.1f);
            Assert.AreEqual(0.8f, sheet.Get(StatType.ElementalResistance), TOLERANCE);

            sheet.SetBase(StatType.MaxElementalResistance, 0.5f);
            Assert.AreEqual(0.85f, sheet.Get(StatType.ElementalResistance), TOLERANCE);
        }

        [Test]
        public void Get_CritChance_CappedAt100Percent()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.CritChance, 1.4f);

            Assert.AreEqual(1f, sheet.Get(StatType.CritChance), TOLERANCE);
        }

        [Test]
        public void MainStatDamageMultiplier_NoMainStat_ReturnsOne()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.Strength, 1000f);

            Assert.AreEqual(1f, sheet.MainStatDamageMultiplier, TOLERANCE);
        }

        [Test]
        public void MainStatDamageMultiplier_Strength1000_ReturnsTwo()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.Strength, 1000f);
            sheet.SetMainStat(StatType.Strength);

            Assert.AreEqual(2f, sheet.MainStatDamageMultiplier, TOLERANCE);
        }

        [Test]
        public void GetMitigation_Physical_CombinesArmorAndDamageReduction()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.Armor, 1000f);
            sheet.SetBase(StatType.DamageReduction, 0.2f);

            // 방어력 50% + 전체 20% → 1 − 0.5 × 0.8 = 0.6
            Assert.AreEqual(0.6f, sheet.GetMitigation(DamageType.Physical), TOLERANCE);
        }

        [Test]
        public void AddAndRemoveSource_RaisesChangedAndRestoresValue()
        {
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.MaxHealth, 500f);
            TestSource source = new TestSource();
            source.Modifiers.Add(new StatModifier(StatType.MaxHealth, ModifierType.Flat, 100f));
            int changedCount = 0;
            sheet.Changed += () => changedCount++;

            sheet.AddSource(source);
            Assert.AreEqual(600f, sheet.Get(StatType.MaxHealth), TOLERANCE);

            sheet.RemoveSource(source);
            Assert.AreEqual(500f, sheet.Get(StatType.MaxHealth), TOLERANCE);
            Assert.AreEqual(2, changedCount);
        }
    }
}
