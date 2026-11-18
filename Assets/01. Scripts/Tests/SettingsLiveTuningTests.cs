using System.Reflection;
using CardShop.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// CardRevealer의 부품별 설정 묶음(Settings)은 부품이 객체를 그대로 참조합니다.
    /// 인스펙터에서 값을 고쳐도(SerializedObject 적용) 유니티가 묶음 객체를 새로 만들지 않고 그 자리에서 바꿔야
    /// 이미 만들어진 부품에도 Play 중 조정이 반영됩니다. 그 전제를 고정합니다.
    /// </summary>
    public class SettingsLiveTuningTests
    {
        [Test]
        public void InspectorEdit_UpdatesSameSettingsObject()
        {
            var go = new GameObject("RevealerTest");
            try
            {
                var revealer = go.AddComponent<CardRevealer>();
                FieldInfo field = typeof(CardRevealer).GetField("pileSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "pileSettings 필드 없음");
                object before = field.GetValue(revealer);

                var so = new SerializedObject(revealer);
                so.FindProperty("pileSettings.FlySeconds").floatValue = 0.9f;
                so.ApplyModifiedPropertiesWithoutUndo();

                object after = field.GetValue(revealer);
                Assert.AreSame(before, after, "인스펙터 수정 뒤에도 같은 묶음 객체여야 부품에 반영됨");
                Assert.AreEqual(0.9f, ((CounterPileView.Settings)after).FlySeconds, 1e-5f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
