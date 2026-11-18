using System.Collections.Generic;
using System.Reflection;
using CardShop.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>Shop 씬 연결 상태 검사. 씬을 손으로 고치다 참조가 빠지면 여기서 잡힙니다.</summary>
    public class SceneSetupTests
    {
        const string ScenePath = "Assets/00. Scenes/Shop.unity";

        /// <summary>
        /// 씬의 모든 CardShop 컴포넌트에서 [SerializeField] 오브젝트 참조(배열 칸 포함)가 비어 있지 않은지 한 번에 검사합니다.
        /// 필드를 새로 추가해도 테스트를 고칠 필요가 없습니다. 비워 둬도 되는 필드는 Tooltip에 "비워"라고 적거나 AudioClip이면 건너뜁니다.
        /// </summary>
        [Test]
        public void ShopScene_AllCardShopReferencesAreConnected()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var missing = new List<string>();
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("CardShop")) continue;
                SerializedProperty it = new SerializedObject(mb).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null) continue;
                    if (it.name == "m_Script" || IsOptional(mb.GetType(), it.propertyPath)) continue;
                    missing.Add($"{mb.gameObject.name}/{mb.GetType().Name}.{it.propertyPath}");
                }
            }
            Assert.IsEmpty(missing, "미연결 참조 (Build Shop Scene 다시 실행):\n" + string.Join("\n", missing));
        }

        // 검사 대상: CardShop 클래스가 직접 선언한 필드만 (XRI 부모 클래스 필드 · UnityEvent 내부 값은 제외).
        // 비워 둬도 되는 필드: AudioClip, 또는 Tooltip에 "비우면/비워 두면"이라고 적힌 필드.
        static bool IsOptional(System.Type type, string propertyPath)
        {
            if (propertyPath.Contains("m_PersistentCalls")) return true;
            string root = propertyPath.Split('.')[0];
            for (System.Type t = type; t != null && t.Namespace != null && t.Namespace.StartsWith("CardShop"); t = t.BaseType)
            {
                FieldInfo f = t.GetField(root, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f == null) continue;
                if (typeof(AudioClip).IsAssignableFrom(f.FieldType)) return true;
                var tip = f.GetCustomAttribute<TooltipAttribute>();
                return tip != null && (tip.tooltip.Contains("비워") || tip.tooltip.Contains("비우면"));
            }
            return true;
        }

        /// <summary>
        /// 계산 확장 4 · 5단계: 계산대에 봉투가 연결되어 있고, 팔리는 품목 프리팹은 모두 찍고(ScannableItem) 잡을 수 있음(잡기 + 물리 없는 Rigidbody).
        /// 봉투 칸은 예전 씬 호환으로 비워도 되게 해 두어서 위 통합 테스트가 잡지 못하므로 따로 검사합니다.
        /// </summary>
        [Test]
        public void ShopScene_CheckoutHasBagAndGrabbableItems()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var counter = Object.FindAnyObjectByType<CardShop.Customers.CheckoutCounter>();
            Assert.IsNotNull(counter, "계산대 없음");
            var so = new SerializedObject(counter);
            Assert.IsNotNull(so.FindProperty("bag").objectReferenceValue, "종이봉투 미연결 (Build Shop Scene 다시 실행)");

            var config = so.FindProperty("shopItems").objectReferenceValue as CardShop.Cards.ShopItemConfig;
            Assert.IsNotNull(config, "품목 설정 미연결");
            var prefabs = new List<CardShop.Customers.ScannableItem>();
            foreach (CardShop.Cards.ShopItemConfig.Item item in config.items) prefabs.Add(item.prefab);
            prefabs.Add(so.FindProperty("cardItemPrefab").objectReferenceValue as CardShop.Customers.ScannableItem);
            foreach (CardShop.Customers.ScannableItem p in prefabs)
            {
                Assert.IsNotNull(p, "품목 프리팹이 비어 있음");
                Assert.IsNotNull(p.GetComponent("XRGrabInteractable"), $"{p.name}: 잡기 없음");
                var rb = p.GetComponent<Rigidbody>();
                Assert.IsTrue(rb != null && rb.isKinematic && !rb.useGravity, $"{p.name}: 물리 없는(kinematic) Rigidbody가 아님");
                Assert.IsNotNull(p.GetComponent<BoxCollider>(), $"{p.name}: 콜라이더 없음 (봉투에 쌓을 높이 · 스캐너 판정)");
            }
        }

        [Test]
        public void ShopScene_HasOneGameContextWithFlow()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameContext[] contexts = Object.FindObjectsByType<GameContext>(FindObjectsSortMode.None);
            Assert.AreEqual(1, contexts.Length, "GameContext는 씬에 하나");

            Assert.IsNotNull(contexts[0].GetComponent<GameFlow>(), "GameFlow 없음");
        }

        [Test]
        public void ShopScene_HasShelfPackSystemAndButtons()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var shelf = Object.FindAnyObjectByType<CardShop.Interaction.ShelfController>();
            Assert.IsNotNull(shelf, "팩 선반 없음");
            var so = new SerializedObject(shelf);
            Assert.IsNotNull(so.FindProperty("packPrefab").objectReferenceValue, "선반 팩 프리팹 미연결");
            Assert.IsNotNull(so.FindProperty("opener").objectReferenceValue, "선반 PackOpener 미연결");
            Assert.Greater(so.FindProperty("slots").arraySize, 0, "선반 칸 없음");

            var revealer = Object.FindAnyObjectByType<CardShop.Presentation.CardRevealer>();
            Assert.IsNotNull(revealer, "CardRevealer 없음");
            var revealerSo = new SerializedObject(revealer);
            Assert.IsNotNull(revealerSo.FindProperty("cardPrefab").objectReferenceValue, "카드 프리팹 미연결");
            Assert.IsNotNull(revealerSo.FindProperty("revealProfile").objectReferenceValue, "등급별 연출 프로필 미연결");

            CardShop.Interaction.PokeButton[] buttons = Object.FindObjectsByType<CardShop.Interaction.PokeButton>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(buttons.Length, 2, "상자·퇴근 버튼");
            foreach (var b in buttons)
                Assert.Greater(b.pressed.GetPersistentEventCount(), 0, $"{b.name}: 연결된 동작 없음");
        }

        // 퇴근 후 집: 전환 연출 · 집 구역 · 침대 버튼이 연결돼 있어야 하루가 이어짐
        [Test]
        public void ShopScene_HasHomeTransitionAndSleepButton()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var transition = Object.FindAnyObjectByType<CardShop.Presentation.DayTransition>();
            Assert.IsNotNull(transition, "DayTransition 없음 (Build Shop Scene 다시 실행)");
            var sleep = GameObject.Find("Button_Sleep");
            Assert.IsNotNull(sleep, "잠자기 버튼 없음");
            Assert.Greater(sleep.GetComponent<CardShop.Interaction.PokeButton>().pressed.GetPersistentEventCount(), 0, "잠자기 버튼에 연결된 동작 없음");
        }

        // 손님: 동선 · 외형 목록 · 결제 버튼이 연결돼 있어야 영업 중 손님이 오고 결제가 됨
        [Test]
        public void ShopScene_HasCustomerDirectorAndPayButton()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var director = Object.FindAnyObjectByType<CardShop.Customers.CustomerDirector>();
            Assert.IsNotNull(director, "CustomerDirector 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(director);
            Assert.Greater(so.FindProperty("browseSpots").arraySize, 0, "구경 자리 없음");
            Assert.Greater(so.FindProperty("queueSlots").arraySize, 0, "계산대 줄 자리 없음");

            var roster = (CardShop.Cards.CustomerRoster)so.FindProperty("roster").objectReferenceValue;
            Assert.Greater(roster.entries.Count, 0, "손님 외형 목록이 비어 있음");
            Assert.IsNotNull(roster.maleController, "남성 Animator 미연결");
            Assert.IsNotNull(roster.femaleController, "여성 Animator 미연결");

            // 바코드 계산: 계산대(물건 자리 · 품목 설정 · 카드 프리팹 · 포스기) · 스캐너 (계산시스템_확장_설계)
            var counter = (CardShop.Customers.CheckoutCounter)so.FindProperty("counter").objectReferenceValue;
            Assert.IsNotNull(counter, "CustomerDirector.counter 미연결");
            var counterSo = new SerializedObject(counter);
            Assert.Greater(counterSo.FindProperty("itemSpots").arraySize, 0, "계산대 물건 자리 없음");
            var shopItems = (CardShop.Cards.ShopItemConfig)counterSo.FindProperty("shopItems").objectReferenceValue;
            Assert.Greater(shopItems.Options().Count, 0, "팔 수 있는 가게 재고가 없음 (품목 프리팹 확인)");
            var scanner = Object.FindAnyObjectByType<CardShop.Customers.BarcodeScanner>();
            Assert.IsNotNull(scanner, "바코드 스캐너 없음");
            var scannerSo = new SerializedObject(scanner);
            Assert.IsNotNull(scannerSo.FindProperty("tip").objectReferenceValue, "스캐너 tip 미연결");

            var pay = GameObject.Find("Button_Pay");
            Assert.IsNotNull(pay, "결제 버튼 없음");
            Assert.Greater(pay.GetComponent<CardShop.Interaction.PokeButton>().pressed.GetPersistentEventCount(), 0, "결제 버튼에 연결된 동작 없음");
        }

        // 청소 2종 (기획서 5-1, DEC-014): 쓰레기 원본 · 팩 꽂이가 연결되고, 잡는 물건은 잡기 + 물리 없는 Rigidbody
        [Test]
        public void ShopScene_HasCleanupDirectorWithTrashAndPackRack()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var director = Object.FindAnyObjectByType<CardShop.Cleanup.CleanupDirector>();
            Assert.IsNotNull(director, "CleanupDirector 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(director);

            SerializedProperty trash = so.FindProperty("trashTemplates");
            Assert.Greater(trash.arraySize, 0, "쓰레기 원본 없음");
            for (int i = 0; i < trash.arraySize; i++)
            {
                var t = (CardShop.Cleanup.TrashSpot)trash.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.IsFalse(t.gameObject.activeSelf, $"{t.name}: 원본은 꺼 둬야 함 (켜 두면 영업 전부터 바닥에 보임)");
                AssertGrabbable(t.gameObject);
            }

            var rack = (CardShop.Cleanup.PackRack)so.FindProperty("rack").objectReferenceValue;
            var rackSo = new SerializedObject(rack);
            Assert.AreEqual(ShopLayout.PackRackSlots, rackSo.FindProperty("slots").arraySize, "팩 꽂이 칸 수");
            Assert.AreEqual(rackSo.FindProperty("slots").arraySize, rackSo.FindProperty("packs").arraySize, "칸마다 팩 하나");
            for (int i = 0; i < rackSo.FindProperty("packs").arraySize; i++)
                AssertGrabbable(((Component)rackSo.FindProperty("packs").GetArrayElementAtIndex(i).objectReferenceValue).gameObject);
        }

        static void AssertGrabbable(GameObject go)
        {
            Assert.IsNotNull(go.GetComponent<CardShop.Cleanup.HeldItem>(), $"{go.name}: HeldItem 없음");
            Assert.IsNotNull(go.GetComponent("XRGrabInteractable"), $"{go.name}: 잡기 없음");
            var rb = go.GetComponent<Rigidbody>();
            Assert.IsTrue(rb != null && rb.isKinematic && !rb.useGravity, $"{go.name}: 물리 없는(키네마틱) Rigidbody여야 함");
            Assert.IsNotNull(go.GetComponent<Collider>(), $"{go.name}: 잡기 판정 콜라이더 없음");
        }

        // 바인더: 칸 · 페이지 버튼 · 등급 색 · 표지 소켓이 연결돼 있고, 표지 소켓과 카드가 같은 Interaction Layer(Card)를 써야 카드가 꽂힘
        [Test]
        public void ShopScene_HasBinderWithSlotsButtonsAndCoverSocket()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var binder = Object.FindAnyObjectByType<CardShop.Presentation.BinderView>();
            Assert.IsNotNull(binder, "BinderView 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(binder);
            SerializedProperty slots = so.FindProperty("slots");
            Assert.Greater(slots.arraySize, 0, "바인더 칸 없음");
            for (int i = 0; i < slots.arraySize; i++)
                Assert.IsNotNull(slots.GetArrayElementAtIndex(i).objectReferenceValue, $"바인더 칸 {i} 미연결");

            const long cardLayer = 1L << CardShop.Interaction.CardInteractionLayers.CardIndex;
            var socket = new SerializedObject(so.FindProperty("coverSocket").objectReferenceValue);
            Assert.AreEqual(cardLayer, socket.FindProperty("m_InteractionLayers.m_Bits").longValue, "표지 소켓은 Card 층만");

            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03. Prefabs/Cards/Card.prefab");
            Assume.That(card, Is.Not.Null, "카드 프리팹 없음");
            var grab = new SerializedObject(card.GetComponent("XRGrabInteractable"));
            Assert.AreNotEqual(0L, grab.FindProperty("m_InteractionLayers.m_Bits").longValue & cardLayer, "카드가 Card 층에 없어 바인더에 안 꽂힘 (Build Card Prefab 다시 실행)");
        }

        // BUG-001: 스크립트가 빠진 컴포넌트는 에러 없이 조용히 동작을 멈춥니다.
        [Test]
        public void PackPrefab_HasNoMissingScriptsAndOpenMethod()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03. Prefabs/Packs/Pack.prefab");
            Assume.That(prefab, Is.Not.Null, "팩 프리팹 없음");
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), $"{t.name}: 스크립트 없는 컴포넌트");
            Assert.IsNotNull(prefab.GetComponent<CardShop.Interaction.IPackOpenMethod>(), "개봉 방식(IPackOpenMethod) 없음");
        }

        // 시연 피드백 A4: 뜯김 연출이 윗 압착 띠 조각을 이름으로 찾습니다. 이름이 바뀌면 띠가 날아가지 않고 팩에 남습니다.
        [Test]
        public void PackPrefab_HasTearEffectAndTopStripParts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03. Prefabs/Packs/Pack.prefab");
            Assume.That(prefab, Is.Not.Null, "팩 프리팹 없음");
            Assert.IsNotNull(prefab.GetComponent<CardShop.Presentation.PackTearEffect>(), "PackTearEffect 없음 (Build Pack Prefab 다시 실행)");
            Transform visual = prefab.transform.Find("Visual");
            Assert.IsNotNull(visual, "Visual 없음");
            Assert.IsNotNull(visual.Find("SealTop"), "윗 압착 띠(SealTop) 없음");
            Assert.IsNotNull(visual.Find("FoldHinge"), "접힘 모서리(FoldHinge) 없음");
        }

        [Test]
        public void ShopScene_IsFirstBuildScene()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Length > 0);
            Assert.AreEqual(ScenePath, EditorBuildSettings.scenes[0].path);
        }
    }
}
