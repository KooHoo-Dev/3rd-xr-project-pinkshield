using System;
using System.Collections;
using CardShop.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 방에 도착한 택배 상자 하나 (DEC-040 U5: 사면 그날 밤 택배 → 잡아서 열면 가구가 바뀜).
    /// 잡는 순간(레이 · 손) 상자가 통통 튀며 작아져 사라지고 Opened를 알립니다. 위에 무엇이 들었는지 글자로 띄웁니다.
    /// 만들고 지우는 것은 RoomUpgradeView가 합니다.
    /// </summary>
    public sealed class DeliveryBox : MonoBehaviour
    {
        const float OpenSeconds = 0.45f;
        static readonly Color Cardboard = new Color(0.72f, 0.53f, 0.33f);
        static readonly Vector3 FallbackSize = new Vector3(0.4f, 0.3f, 0.4f);

        public UpgradeKind Kind { get; private set; }
        /// <summary>상자를 열었을 때 (한 번만)</summary>
        public event Action<DeliveryBox> Opened;

        XRGrabInteractable grab;
        Transform label;
        bool opening;

        /// <summary>parent 아래 world 위치에 상자를 놓습니다. model이 없으면 갈색 상자.</summary>
        public static DeliveryBox Create(Transform parent, Vector3 worldPosition, float yaw, UpgradeKind kind, string label,
            GameObject model, Material tintSource, TMP_FontAsset font)
        {
            var root = new GameObject($"DeliveryBox_{kind}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, yaw, 0f));

            GameObject look;
            if (model != null)
            {
                look = Instantiate(model, root.transform);
                look.transform.localPosition = Vector3.zero;
                look.transform.localRotation = Quaternion.identity;
                HomeRoom.RemoveColliders(look);
                Bounds lb = HomeRoom.Bounds(look);
                look.transform.position += worldPosition - new Vector3(lb.center.x, lb.min.y, lb.center.z); // 바닥에 앉힘
            }
            else
            {
                look = Core.RuntimeShapes.Create(PrimitiveType.Cube, "Look", root.transform, new Vector3(0f, FallbackSize.y / 2f, 0f), FallbackSize,
                    HomeRoom.Tinted(tintSource, Cardboard));
            }
            look.name = "Look";

            // 잡기 판정은 보이는 모양의 경계 상자 하나로
            Bounds b = HomeRoom.Bounds(look);
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(b.center);
            col.size = b.size;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var box = root.AddComponent<DeliveryBox>();
            box.Kind = kind;
            box.grab = root.AddComponent<XRGrabInteractable>();
            box.grab.throwOnDetach = false;
            box.grab.selectEntered.AddListener(box.OnGrabbed);
            box.AddLabel(label, b.max.y - worldPosition.y + 0.12f, font);
            return box;
        }

        void AddLabel(string text, float height, TMP_FontAsset font)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = $"<mark=#3B2B59CC> 택배: {text} </mark>\n<size=70%>잡으면 열려요</size>";
            tmp.fontSize = 0.9f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(1f, 0.3f);
            label = go.transform;
        }

        // 글자가 늘 플레이어 쪽을 보게 (VR에서 뒤에서 읽어도 뒤집히지 않게)
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (label == null || cam == null) return;
            Vector3 away = label.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f) label.rotation = Quaternion.LookRotation(away);
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (opening) return;
            opening = true;
            // 손에서 바로 떼어 냄 (상자는 열리면서 사라짐)
            if (grab.interactionManager != null) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = false;
            Sfx.Play(SoundId.PackTear, transform.position);
            // 살짝 부풀었다가 작아져 사라짐 (11-21 2차 V5: DOTween, PopIn)
            PopIn.PopOut(transform, OpenSeconds, () =>
            {
                Opened?.Invoke(this);
                Destroy(gameObject);
            });
        }
    }
}
