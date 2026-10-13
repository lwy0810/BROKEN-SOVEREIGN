using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using NUnit.Framework;
using UnityEngine;

namespace BrokenSovereign.Tests
{
    public class CharacterStatsDataTests
    {
        private CharacterStatsData _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<CharacterStatsData>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void GetBaseValue_CoversEveryStatType()
        {
            foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            {
                Assert.DoesNotThrow(() => _data.GetBaseValue(stat), $"{stat} 필드 누락");
            }
        }

        [Test]
        public void ApplyTo_WritesBaseValuesToSheet()
        {
            var sheet = new StatSheet();

            _data.ApplyTo(sheet);

            Assert.AreEqual(500f, sheet.GetBase(StatType.MaxHealth), 0.0001f);
            Assert.AreEqual(100f, sheet.GetBase(StatType.AttackPower), 0.0001f);
            Assert.AreEqual(0.05f, sheet.GetBase(StatType.CritChance), 0.0001f);
            Assert.AreEqual(1f, sheet.MainStatDamageMultiplier, 0.0001f);
        }
    }
}
