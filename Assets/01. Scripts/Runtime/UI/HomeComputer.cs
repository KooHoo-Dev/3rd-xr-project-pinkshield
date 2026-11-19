using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.UI
{
    /// <summary>
    /// 집 컴퓨터 모니터 한 대에 [커뮤니티 · 슬라임마켓] 두 쪽 (집_컴퓨터_화면_설계 J1, Q2 · Q3 · Q5 추천안).
    /// 오른쪽 벽에 따로 있던 슬라임마켓을 모니터 앞으로 옮기고, 모니터 양옆 [◀ 커뮤니티] · [슬라임마켓 ▶] 버튼이나
    /// 레이로 화면을 잡고 좌우로 끌어(ScreenSwipeArea → PagerRules) 넘깁니다. 넘길 때 0.25초 옆으로 미끄러지고 딸깍 + 약한 진동.
    /// 집에 오면 늘 커뮤니티 쪽부터. 커뮤니티 화면은 씬의 3D 글자(CommunityScreen)를 그대로 쓰고, 숨길 때는 크기를 0으로,
    /// 마켓(캔버스)은 CanvasGroup으로 숨깁니다 — HomePanel이 집 페이즈마다 content를 켜고 끄므로 그 위에서 따로 감춤.
    /// 안 한 것(아침 검토): 책상 앞 손으로 직접 밀기(J1-c), 상품 카드 격자 · 그림(F8), ComputerScreenAnchor(Q9).
    /// </summary>
    public sealed class HomeComputer : PhaseListener
    {
        const int PageCount = 2;
        const float SlideSeconds = 0.25f;
        const float SlideDistance = 0.35f; // m

        Transform community;
        Vector3 communityPos, communityScale;
        RectTransform market;
        CanvasGroup marketGroup;
        Vector3 marketPos;
        GameObject sideButtons, communitySwipe;
        Button prevButton, nextButton;
        int page;
        float slideT = 1f;
        int slideDir;

        /// <summary>모니터에 마켓을 붙입니다. 커뮤니티 화면이 없으면 false (호출한 쪽이 예전처럼 벽에 둠)</summary>
        public static bool Create(UpgradeCatalog catalog, TMP_FontAsset font)
        {
            var screen = FindAnyObjectByType<CommunityScreen>(FindObjectsInactive.Include);
            if (screen == null) return false;
            Transform root = screen.transform;
            var go = new GameObject("HomeComputer");
            go.transform.SetParent(root.parent, false);
            var pc = go.AddComponent<HomeComputer>();
            pc.Build(root, catalog, font);
            return true;
        }

        void Build(Transform communityRoot, UpgradeCatalog catalog, TMP_FontAsset font)
        {
            community = communityRoot;
            communityPos = community.localPosition;
            communityScale = community.localScale;
            Quaternion rot = community.localRotation;
            float yaw = rot.eulerAngles.y - 180f; // MarketScreen.Create는 yaw + 180으로 앞면(-Z)을 돌림

            // 마켓: 모니터 앞, 커뮤니티 글자보다 1cm 앞
            Vector3 forward = rot * Vector3.back; // 화면이 보는 쪽 (-Z)
            marketPos = communityPos + forward * 0.01f;
            MarketScreen screen = MarketScreen.Create(community.parent, marketPos, yaw, catalog, font);
            market = (RectTransform)screen.transform;
            marketGroup = market.gameObject.AddComponent<CanvasGroup>();
            market.gameObject.AddComponent<ScreenSwipeArea>().Swiped += OnSwiped;
            // 마켓 바탕 · 카드는 레이를 받지 않으므로, 빈 곳에서 시작한 끌기도 받게 맨 뒤에 투명 판 (버튼은 그 앞이라 그대로 눌림)
            if (market.childCount > 0)
            {
                Image hit = WorldUiKit.AddImage(market.GetChild(0), "SwipeHit", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
                hit.transform.SetAsFirstSibling();
            }

            // 커뮤니티 쪽 끌기 판: 글자 앞의 투명 판 (모니터 크기)
            RectTransform swipe = WorldUiKit.CreateCanvas("CommunitySwipe", community.parent, 1300f, 900f, out GameObject swipeContent);
            swipe.localPosition = communityPos + forward * 0.005f;
            swipe.localRotation = rot;
            WorldUiKit.AddImage(swipeContent.transform, "Hit", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            swipe.gameObject.AddComponent<ScreenSwipeArea>().Swiped += OnSwiped;
            communitySwipe = swipe.gameObject;

            // 모니터 양옆 버튼 (탭 겸 화살표)
            sideButtons = new GameObject("PageButtons");
            sideButtons.transform.SetParent(community.parent, false);
            prevButton = SideButton(rot, forward, -0.78f, "◀\n커뮤\n니티", font, () => GoTo(0));
            nextButton = SideButton(rot, forward, 0.78f, "슬라\n임\n마켓\n▶", font, () => GoTo(1));
            Refresh(); // AddComponent 때(OnEnable)는 아직 화면이 없어 첫 갱신이 비었음
        }

        Button SideButton(Quaternion rot, Vector3 forward, float side, string text, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rt = WorldUiKit.CreateCanvas("Page_" + (side < 0 ? "Prev" : "Next"), sideButtons.transform, 160f, 420f, out GameObject content);
            rt.localPosition = communityPos + rot * new Vector3(side, 0f, 0f) + forward * 0.01f;
            rt.localRotation = rot;
            Button b = WorldUiKit.AddButton(content.transform, text, Vector2.zero, Vector2.one, font);
            b.onClick.AddListener(onClick);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = 34f;
            return b;
        }

        void OnSwiped(float dx, float width)
        {
            int next = PagerRules.AfterDrag(page, PageCount, dx, width);
            if (next != page) GoTo(next);
        }

        /// <summary>쪽으로 넘김 (0 = 커뮤니티, 1 = 슬라임마켓)</summary>
        public void GoTo(int target)
        {
            target = PagerRules.Clamp(target, PageCount);
            if (target == page) return;
            slideDir = target > page ? 1 : -1;
            page = target;
            slideT = 0f;
            Sfx.Play(SoundId.UiClick);
            foreach (XRBaseInputInteractor hand in FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
                hand.SendHapticImpulse(0.2f, 0.04f);
            ApplyPage(false);
        }

        protected override void Refresh()
        {
            bool home = CurrentPhase == GamePhaseId.Home;
            if (sideButtons != null) sideButtons.SetActive(home);
            if (home && page != 0) { page = 0; slideT = 1f; } // 집에 오면 커뮤니티부터 (Q5)
            ApplyPage(true);
        }

        void Update()
        {
            if (slideT >= 1f) return;
            slideT = Mathf.Min(1f, slideT + Time.unscaledDeltaTime / SlideSeconds);
            ApplyPage(false);
        }

        // 지금 쪽은 가운데로 미끄러져 들어오고, 다른 쪽은 반대로 나가며 사라짐
        void ApplyPage(bool instant)
        {
            if (community == null || market == null) return;
            float t = instant ? 1f : 1f - (1f - slideT) * (1f - slideT);
            Vector3 right = community.localRotation * Vector3.right;
            float inFrom = slideDir * SlideDistance * (1f - t);
            bool onCommunity = page == 0;

            community.localPosition = communityPos + right * (onCommunity ? inFrom : -slideDir * SlideDistance * t);
            float cs = onCommunity ? t : 1f - t;
            community.localScale = communityScale * (instant ? (onCommunity ? 1f : 0f) : Mathf.Max(0.0001f, cs));

            market.localPosition = marketPos + right * (onCommunity ? -slideDir * SlideDistance * t : inFrom);
            marketGroup.alpha = instant ? (onCommunity ? 0f : 1f) : (onCommunity ? 1f - t : t);
            marketGroup.blocksRaycasts = marketGroup.interactable = !onCommunity;
            if (communitySwipe != null) communitySwipe.SetActive(onCommunity && CurrentPhase == GamePhaseId.Home);
            if (prevButton != null) prevButton.interactable = !onCommunity;
            if (nextButton != null) nextButton.interactable = onCommunity;
        }
    }
}
