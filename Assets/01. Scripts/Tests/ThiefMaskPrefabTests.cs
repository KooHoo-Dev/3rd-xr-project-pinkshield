using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>도둑 복면 프리팹 (ThiefMaskBuilder): 원점 = 머리 가운데, +Z = 얼굴 앞, 손님 머리 크기.</summary>
    public class ThiefMaskPrefabTests
    {
        const string PrefabPath = "Assets/03. Prefabs/Customers/ThiefMask.prefab";

        static Bounds RootSpaceBounds(out MeshFilter filter)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            filter = prefab.GetComponentInChildren<MeshFilter>();
            Assert.IsNotNull(filter?.sharedMesh, "복면 메시");
            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 m = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? -1 : 1, (i / 2) % 2 == 0 ? -1 : 1, i / 4 == 0 ? -1 : 1);
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, corner)));
            }
            return b;
        }

        [Test]
        public void Mask_IsHeadSized()
        {
            Bounds b = RootSpaceBounds(out _);
            // 11-20 사용자가 Play에서 너무 커 보여 크기 1.05 → 0.8로 줄임 (예전 범위 × 0.8/1.05)
            Assert.That(b.size.x, Is.InRange(0.21f, 0.31f), "좌우 폭");
            Assert.That(b.size.z, Is.InRange(0.22f, 0.34f), "앞뒤 깊이");
            Assert.That(b.max.y, Is.InRange(0.03f, 0.13f), "머리 가운데에서 정수리까지");
        }

        [Test]
        public void Mask_CentersOnHead_FaceTowardPlusZ()
        {
            Bounds b = RootSpaceBounds(out MeshFilter filter);
            Assert.That(Mathf.Abs(b.center.x), Is.LessThan(0.02f), "좌우 가운데");
            // 모델 얼굴은 메시 -X 쪽 (FBX +X를 Unity가 뒤집음) → 프리팹에서 +Z를 봐야 함
            Vector3 face = filter.transform.localRotation * Vector3.left;
            Assert.That(Vector3.Dot(face, Vector3.forward), Is.GreaterThan(0.99f), "얼굴이 +Z");
        }

        [Test]
        public void Mask_UsesUrpLitWithTexture()
        {
            RootSpaceBounds(out MeshFilter filter);
            Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.IsNotNull(mat);
            Assert.AreEqual("Universal Render Pipeline/Lit", mat.shader.name);
            Assert.IsNotNull(mat.GetTexture("_BaseMap"), "니트 텍스처");
        }
    }
}
