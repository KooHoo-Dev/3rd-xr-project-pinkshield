using CardShop.Cleanup;
using CardShop.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 청소 2종(기획서 5-1, 창문 얼룩은 DEC-014로 제외)에 필요한 씬 오브젝트를 만듭니다. ShopSceneBuilder.Build가 손님 동선 다음에 호출합니다.
    ///   바닥 쓰레기: 쓰레기통 판정(인테리어 Trash_01 자리) + 쓰레기 모양 원본 3종(꺼 둠, CleanupDirector가 복제)
    ///   흐트러진 팩: 판매 진열대 위 팩 꽂이(4칸) + 꽂혀 있는 팩
    /// 모양은 기본 도형(가볍고 Quest에서 그림자 없음). 위치 값은 ShopLayout.
    /// </summary>
    public static class CleanupBuilder
    {
        // 팩 꽂이 팩 크기 (계산대 카드팩과 비슷하게, m)
        static readonly Vector3 PackSize = new Vector3(0.07f, 0.115f, 0.012f);
        static readonly Color[] PackColors =
        {
            new Color(1f, 0.55f, 0.7f), new Color(0.45f, 0.7f, 1f), new Color(1f, 0.85f, 0.35f), new Color(0.5f, 0.85f, 0.5f),
        };

        public static void Create(GameFlow flow, TMP_FontAsset font)
        {
            var root = new GameObject("Cleanup").transform;

            // ---- 바닥 쓰레기
            var binGo = new GameObject("TrashBinZone");
            binGo.transform.SetParent(root);
            binGo.transform.position = ShopLayout.TrashBinPos;
            TrashBin bin = binGo.AddComponent<TrashBin>();
            CustomerFloorBuilder.Label(root, "쓰레기통", ShopLayout.TrashBinPos + new Vector3(0f, 0.75f, 0f), font, 0.3f);
            bin.Configure(BinHint(binGo.transform, font));

            var templates = new GameObject("Templates (꺼 둔 원본)").transform;
            templates.SetParent(root);
            TrashSpot[] trash =
            {
                Trash(templates, "Trash_Paper", PrimitiveType.Sphere, new Vector3(0.08f, 0.07f, 0.075f), Quaternion.identity, new Color(0.92f, 0.92f, 0.88f)),
                Trash(templates, "Trash_Can", PrimitiveType.Cylinder, new Vector3(0.066f, 0.06f, 0.066f), Quaternion.Euler(0f, 0f, 90f), new Color(0.85f, 0.2f, 0.2f)),
                Trash(templates, "Trash_Wrapper", PrimitiveType.Cube, new Vector3(0.13f, 0.012f, 0.09f), Quaternion.Euler(0f, 0f, 4f), new Color(1f, 0.8f, 0.2f)),
            };

            // ---- 흐트러진 팩
            PackRack rack = Rack(root, font);

            var director = root.gameObject.AddComponent<CleanupDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("bin").objectReferenceValue = bin;
            SetArray(so.FindProperty("trashTemplates"), trash);
            so.FindProperty("rack").objectReferenceValue = rack;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 꺼 둔 원본: 바닥에 닿는 높이(y)와 기본 자세만 의미 있음. x · z는 복제할 때 정함
        static TrashSpot Trash(Transform parent, string name, PrimitiveType shape, Vector3 size, Quaternion rot, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            // 누운 캔은 x 크기가 높이가 됨
            float height = shape == PrimitiveType.Cylinder ? size.x : size.y;
            go.transform.SetPositionAndRotation(new Vector3(0f, height / 2f, 0f), rot);
            Visual(go.transform, shape, size, color);
            // 잡기 판정은 모양보다 크게 (바닥의 작은 물건을 레이저로 맞히기 쉽게)
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.09f;
            MakeGrabbable(go);
            TrashSpot spot = go.AddComponent<TrashSpot>();
            go.SetActive(false);
            return spot;
        }

        // 판매 진열대 윗면 뒤쪽 절반에 받침 + 칸마다 세운 팩 (앞면이 -Z = 손님 · 계산대 쪽, 뒤로 12도 기댐)
        static PackRack Rack(Transform root, TMP_FontAsset font)
        {
            Vector3 top = ShopLayout.StandPos + new Vector3(0f, ShopLayout.StandSize.y, ShopLayout.StandSize.z * 0.2f);
            var rackGo = new GameObject("PackRack");
            rackGo.transform.SetParent(root);
            rackGo.transform.position = top;

            int n = ShopLayout.PackRackSlots;
            float width = (n - 1) * ShopLayout.PackRackSpacing + 0.2f;
            GameObject board = Visual(rackGo.transform, PrimitiveType.Cube, new Vector3(width, 0.02f, 0.1f), new Color(0.35f, 0.25f, 0.18f));
            board.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            GameObject back = Visual(rackGo.transform, PrimitiveType.Cube, new Vector3(width, 0.08f, 0.012f), new Color(0.35f, 0.25f, 0.18f));
            back.transform.localPosition = new Vector3(0f, 0.05f, 0.045f);

            Quaternion lean = Quaternion.Euler(-12f, 0f, 0f); // 윗부분이 뒤(+Z)로
            var slots = new Transform[n];
            var packs = new RackPack[n];
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) / 2f) * ShopLayout.PackRackSpacing;
                var slot = new GameObject($"Slot_{i}").transform;
                slot.SetParent(rackGo.transform, false);
                slot.localPosition = new Vector3(x, 0.02f + PackSize.y / 2f, 0.01f);
                slot.localRotation = lean;
                slots[i] = slot;

                var pack = new GameObject($"RackPack_{i}");
                pack.transform.SetParent(rackGo.transform, false);
                pack.transform.SetPositionAndRotation(slot.position, slot.rotation);
                Visual(pack.transform, PrimitiveType.Cube, PackSize, PackColors[i % PackColors.Length]);
                var col = pack.AddComponent<BoxCollider>();
                col.size = new Vector3(0.09f, 0.13f, 0.04f); // 바닥에 누웠을 때도 레이저로 맞히기 쉽게 두껍게
                MakeGrabbable(pack);
                packs[i] = pack.AddComponent<RackPack>();
            }

            PackRack rack = rackGo.AddComponent<PackRack>();
            var so = new SerializedObject(rack);
            SetArray(so.FindProperty("slots"), slots);
            SetArray(so.FindProperty("packs"), packs);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 팩을 들고 있을 때: 빈 칸마다 팩 모양 윤곽 + 꽂이 위 안내 글자
            var slotHints = new DropHint[n];
            for (int i = 0; i < n; i++) slotHints[i] = SlotHint(slots[i]);
            rack.ConfigureHints(slotHints, LabelHint(rackGo.transform, new Vector3(0f, 0.3f, 0f), "빈 칸에 꽂기", font));
            return rack;
        }

        // ------------------------------------------------------------------ 놓을 곳 표시 (3차 기기 피드백)

        static readonly Color HintColor = new Color(1f, 0.78f, 0.2f);

        // 쓰레기통: 바닥 빛 고리 + 통 위에서 위아래로 움직이는 표식 + "여기에 버리기"
        static DropHint BinHint(Transform bin, TMP_FontAsset font)
        {
            var hintGo = new GameObject("DropHint");
            hintGo.transform.SetParent(bin, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(hintGo.transform, false);

            GameObject ring = Visual(visual, PrimitiveType.Cylinder, new Vector3(0.7f, 0.003f, 0.7f), HintColor, unlit: true);
            ring.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            var marker = new GameObject("Marker").transform;
            marker.SetParent(visual, false);
            marker.localPosition = new Vector3(0f, 1.0f, 0f);
            GameObject gem = Visual(marker, PrimitiveType.Cube, new Vector3(0.07f, 0.07f, 0.07f), HintColor, unlit: true);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f); // 꼭짓점이 아래를 가리키는 마름모
            CustomerFloorBuilder.Label(marker, "여기에 버리기", marker.position + new Vector3(0f, 0.14f, 0f), font, 0.35f);

            var hint = hintGo.AddComponent<DropHint>();
            hint.Configure(visual.gameObject, new[] { ring.GetComponent<Renderer>(), gem.GetComponent<Renderer>() }, marker);
            return hint;
        }

        // 꽂이 빈 칸: 꽂힐 자리에 팩 크기 윤곽(얇은 테두리 4개)
        static DropHint SlotHint(Transform slot)
        {
            var hintGo = new GameObject("DropHint");
            hintGo.transform.SetParent(slot, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(hintGo.transform, false);
            const float t = 0.008f;
            float w = PackSize.x + 0.02f, h = PackSize.y + 0.02f;
            var edges = new Renderer[4];
            edges[0] = Visual(visual, PrimitiveType.Cube, new Vector3(w, t, t), HintColor, unlit: true).GetComponent<Renderer>();
            edges[0].transform.localPosition = new Vector3(0f, h / 2f, 0f);
            edges[1] = Visual(visual, PrimitiveType.Cube, new Vector3(w, t, t), HintColor, unlit: true).GetComponent<Renderer>();
            edges[1].transform.localPosition = new Vector3(0f, -h / 2f, 0f);
            edges[2] = Visual(visual, PrimitiveType.Cube, new Vector3(t, h, t), HintColor, unlit: true).GetComponent<Renderer>();
            edges[2].transform.localPosition = new Vector3(w / 2f, 0f, 0f);
            edges[3] = Visual(visual, PrimitiveType.Cube, new Vector3(t, h, t), HintColor, unlit: true).GetComponent<Renderer>();
            edges[3].transform.localPosition = new Vector3(-w / 2f, 0f, 0f);

            var hint = hintGo.AddComponent<DropHint>();
            hint.Configure(visual.gameObject, edges, null);
            return hint;
        }

        // 글자 + 작은 마름모 표식만 있는 안내 (꽂이 위)
        static DropHint LabelHint(Transform parent, Vector3 localPos, string text, TMP_FontAsset font)
        {
            var hintGo = new GameObject("DropHint_Label");
            hintGo.transform.SetParent(parent, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(hintGo.transform, false);
            var marker = new GameObject("Marker").transform;
            marker.SetParent(visual, false);
            marker.localPosition = localPos;
            GameObject gem = Visual(marker, PrimitiveType.Cube, new Vector3(0.05f, 0.05f, 0.05f), HintColor, unlit: true);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            CustomerFloorBuilder.Label(marker, text, marker.position + new Vector3(0f, 0.1f, 0f), font, 0.3f);

            var hint = hintGo.AddComponent<DropHint>();
            hint.Configure(visual.gameObject, new[] { gem.GetComponent<Renderer>() }, marker);
            return hint;
        }

        // 물리 없이 잡기 (키네마틱). 카드와 같은 잡기 감각 (3차 기기 피드백: 손에 착 붙는 느낌이 어색):
        // 잡은 자리 · 각도 그대로 들고(동적 어태치), 레이저로 잡으면 레이저 끝에 매달려 옴(카드와 같은 원거리 방식).
        // 놓으면 받는 곳(통 · 꽂이)이 아니면 아래로 떨어져 그 자리에 머묾 (HeldItem)
        internal static void MakeGrabbable(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.farAttachMode = InteractableFarAttachMode.DeferToInteractor;
            go.AddComponent<HeldItem>();
        }

        // 보이는 모양만 (콜라이더 제거). 잡기 판정은 부모 콜라이더가 맡음
        internal static GameObject Visual(Transform parent, PrimitiveType shape, Vector3 size, Color color, bool unlit = false)
        {
            GameObject v = GameObject.CreatePrimitive(shape);
            v.name = "Visual_" + shape;
            v.transform.SetParent(parent, false);
            v.transform.localScale = size;
            Object.DestroyImmediate(v.GetComponent<Collider>());
            var r = v.GetComponent<MeshRenderer>();
            r.sharedMaterial = unlit ? UnlitMat(color) : Mat(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return v;
        }

        // 표시용: 조명과 상관없이 밝게 보이는 색 (DropHint가 _BaseColor를 바꿈)
        static Material UnlitMat(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            return m;
        }

        static Material Mat(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        static void SetArray(SerializedProperty prop, Object[] items)
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
