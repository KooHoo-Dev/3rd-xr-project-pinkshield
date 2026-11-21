using System;
using System.Collections.Generic;
using CardShop.Customers;
using CardShop.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 플레이어 손(컨트롤러 인터랙터)이 지금 잡고 있는 물건을 봅니다. 소켓은 손이 아니므로 뺍니다.
    /// ① 선물 팩 · 빗자루 · 스캐너를 "새로" 잡았으면 신호를 냄 ② 지금 단계와 상관없는 물건을 잡았는지 알려 줌(막지 않고 알려 주기).
    /// 인터랙터 목록은 단계가 시작될 때만 다시 찾습니다(매 프레임 Find 금지).
    /// </summary>
    public sealed class TutorialGrabWatcher
    {
        readonly List<XRBaseInputInteractor> hands = new List<XRBaseInputInteractor>();
        readonly List<Transform> held = new List<Transform>();
        bool heldGift, heldBroom, heldScanner;

        /// <summary>지금 잡고 있는 물건 (Scan 뒤에 채워짐)</summary>
        public IReadOnlyList<Transform> Held => held;

        public void Refresh()
        {
            hands.Clear();
            // 이미 쥐고 있는 물건도 다음 Scan에서 "새로 잡음"으로 셈 — 도둑 안내가 뜰 때 대걸레를 이미 들고 있으면 바로 끝나게 (11-21 2차 B3)
            heldGift = heldBroom = heldScanner = false;
            foreach (XRBaseInputInteractor i in UnityEngine.Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
                hands.Add(i);
        }

        /// <summary>
        /// 잡은 물건을 다시 모으고, 새로 잡은 물건의 신호를 signals에 담습니다(먼저 비움).
        /// isAllowed가 있으면 그것이 false인 물건을 하나라도 잡았을 때 true를 돌려줍니다(엉뚱한 물건).
        /// </summary>
        public bool Scan(List<TutorialSignal> signals, Func<Transform, bool> isAllowed)
        {
            signals.Clear();
            Collect();
            bool gift = false, broom = false, scanner = false, wrong = false;
            foreach (Transform t in held)
            {
                var pack = t.GetComponentInParent<PackView>();
                if (pack != null && pack.IsGift) gift = true;
                if (t.GetComponentInParent<Broom>() != null) broom = true;
                if (t.GetComponentInParent<BarcodeScanner>() != null) scanner = true;
                if (isAllowed != null && !isAllowed(t) && !IsHelper(t)) wrong = true;
            }
            if (gift && !heldGift) signals.Add(TutorialSignal.GiftPackGrabbed);
            if (broom && !heldBroom) signals.Add(TutorialSignal.BroomGrabbed);
            if (scanner && !heldScanner) signals.Add(TutorialSignal.ScannerGrabbed);
            heldGift = gift; heldBroom = broom; heldScanner = scanner;
            return wrong;
        }

        void Collect()
        {
            held.Clear();
            for (int h = 0; h < hands.Count; h++)
            {
                XRBaseInputInteractor hand = hands[h];
                if (hand == null || !hand.hasSelection) continue;
                List<IXRSelectInteractable> selected = hand.interactablesSelected;
                for (int k = 0; k < selected.Count; k++)
                    if (selected[k] is Component c && c != null) held.Add(c.transform);
            }
        }

        /// <summary>
        /// 계산 정지 중에는 스캐너 · 계산대 물건 · 봉투를 함께 쓰므로 엉뚱한 물건으로 치지 않음.
        /// 계산대 물건은 잡는 순간 XRGrabInteractable이 부모를 떼므로(SetParent(null)) CheckoutCounter 자식인지로는 못 알아봄 → ScannableItem으로 확인
        /// </summary>
        public static bool IsHelper(Transform t) =>
            t.GetComponentInParent<ScannableItem>() != null ||
            t.GetComponentInParent<CheckoutCounter>() != null || t.GetComponentInParent<BarcodeScanner>() != null;

        /// <summary>held가 target과 같은 물건이거나 그 자식 · 부모인가</summary>
        public static bool Related(Transform held, Transform target) =>
            held != null && target != null && (held == target || held.IsChildOf(target) || target.IsChildOf(held));
    }
}
