using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 집 컴퓨터 모니터 = "SlimeOS" 하늘색 바탕화면 + 쪽마다 창 하나 [커뮤니티 · 슬라임마켓 · 판매 · 진열]
    /// (집_컴퓨터_화면_설계 J1, 집_책상_하나로_설계 DEC-042, UI_개편_설계 8번 레트로 OS 창).
    /// 아래 작업 표시줄 탭 · 모니터 양옆 [◀] [▶] 버튼 · 레이로 화면을 잡고 좌우로 끌기(ScreenSwipeArea → PagerRules)로 넘깁니다.
    /// 넘길 때 0.25초 옆으로 미끄러지고 딸깍 + 약한 진동. 집에 오면 늘 커뮤니티 쪽부터.
    /// 쪽마다 숨기는 방법은 IComputerPage(CanvasPage)가, 창 모양은 SlimeOsWindow가 맡고, 이 클래스는 쪽 번호 · 넘김 · 화면 틀만.
    /// 판매 · 진열 쪽은 SalesShowcasePage가 AddPage로 붙입니다.
    /// </summary>
    public sealed class HomeComputer : PhaseListener
    {
        public const int CommunityPageIndex = 0;
        // 모니터 1300 × 900mm: 아래 작업 표시줄, 그 위에 창 하나
        public const float ScreenW = 1300f, ScreenH = 900f;
        public const float WindowW = 1260f, WindowH = ScreenH - ComputerTaskbar.HeightPx - 36f;
        const float SlideSeconds = 0.25f;
        const float SlideDistance = 0.35f; // m
        const float ButtonSide = 0.78f;    // 모니터 가운데에서 옆 버튼까지 (m)

        readonly List<IComputerPage> pages = new List<IComputerPage>();
        readonly List<string> names = new List<string>();
        Transform community;
        GameObject chrome; // 집에서만 보이는 바탕화면 · 작업 표시줄 · 옆 버튼
        ComputerTaskbar taskbar;
        Button prevButton, nextButton;
        TMP_Text prevLabel, nextLabel;
        int page, fromPage;
        float slideT = 1f;
        int slideDir;

        /// <summary>모니터 화면이 놓인 부모 · 가운데 자리 · 방향 (쪽을 붙일 때 씀). 화면은 -Z 쪽을 봄</summary>
        public Transform ScreenParent => community.parent;
        public Vector3 ScreenCenter { get; private set; }
        public Quaternion ScreenRotation { get; private set; }
        /// <summary>창 가운데 (위 여백 18mm, 아래 작업 표시줄). 화면 앞으로 depth(m)만큼</summary>
        public Vector3 WindowCenter(float depth) =>
            ScreenCenter + ScreenRotation * new Vector3(0f, (ScreenH * 0.5f - 18f - WindowH * 0.5f) * 0.001f, 0f) + ScreenRotation * Vector3.back * depth;
        public int CurrentPage => page;
        public bool IsHome => CurrentPhase == GamePhaseId.Home;

        /// <summary>모니터에 바탕화면 · 커뮤니티 · 마켓 창을 만듭니다. 커뮤니티 화면이 없으면 null (호출한 쪽이 마켓을 예전처럼 벽에 둠)</summary>
        public static HomeComputer Create(UpgradeCatalog catalog, TMP_FontAsset font)
        {
            var screen = FindAnyObjectByType<CommunityScreen>(FindObjectsInactive.Include);
            if (screen == null) return null;
            var go = new GameObject("HomeComputer");
            go.transform.SetParent(screen.transform.parent, false);
            var pc = go.AddComponent<HomeComputer>();
            pc.Build(screen, catalog, font);
            return pc;
        }

        void Build(CommunityScreen screen, UpgradeCatalog catalog, TMP_FontAsset font)
        {
            community = screen.transform;
            ScreenCenter = community.localPosition;
            ScreenRotation = community.localRotation;
            Quaternion rot = ScreenRotation;
            Vector3 forward = rot * Vector3.back; // 화면이 보는 쪽 (-Z)

            chrome = new GameObject("SlimeOsChrome");
            chrome.transform.SetParent(community.parent, false);

            // 바탕화면 (창 뒤)
            RectTransform desk = WorldUiKit.CreateCanvas("SlimeOsDesktop", chrome.transform, ScreenW, ScreenH, out GameObject deskContent);
            desk.localPosition = ScreenCenter + forward * 0.002f;
            desk.localRotation = rot;
            WorldUiKit.AddImage(deskContent.transform, "Wallpaper", Vector2.zero, Vector2.one, UiTheme.OsDesktop).raycastTarget = false;
            TMP_Text mark = WorldUiKit.AddText(deskContent.transform, "Mark", "SlimeOS", new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), 120f, new Color(1f, 1f, 1f, 0.07f), font);
            mark.alignment = TextAlignmentOptions.Center;

            // 작업 표시줄 (화면 아래)
            Vector3 barPos = ScreenCenter + rot * new Vector3(0f, -(ScreenH - ComputerTaskbar.HeightPx) * 0.0005f + 0.004f, 0f) + forward * 0.014f;
            taskbar = ComputerTaskbar.Create(chrome.transform, barPos, rot, ScreenW - 16f, font);

            // 1쪽: 커뮤니티 창 (씬의 3D 글자 대신)
            RectTransform communityWindow = CommunityWindow.Create(this, screen, font);
            AddSwipe(communityWindow);
            AddPage(new CanvasPage(communityWindow), "커뮤니티");

            // 2쪽: 슬라임마켓 창
            float yaw = rot.eulerAngles.y - 180f; // MarketScreen.Create는 yaw + 180으로 앞면(-Z)을 돌림
            MarketScreen market = MarketScreen.Create(community.parent, WindowCenter(0.01f), yaw, catalog, font);
            var marketRt = (RectTransform)market.transform;
            marketRt.sizeDelta = new Vector2(WindowW, WindowH); // 안의 칸은 비율로 놓여 창 크기를 따라감
            if (marketRt.childCount > 0) SlimeOsWindow.Apply(marketRt.GetChild(0), null, font);
            market.UseLightCards(); // 밝은 창에 맞는 상품 칸 (크림 + 잉크 테)
            AddSwipe(marketRt);
            AddPage(new CanvasPage(marketRt), "슬라임마켓");

            // 모니터 양옆 버튼: 이전 · 다음 쪽 (이름은 갈 쪽)
            prevButton = SideButton(-ButtonSide, font, () => GoTo(page - 1), out prevLabel);
            nextButton = SideButton(ButtonSide, font, () => GoTo(page + 1), out nextLabel);
            Refresh(); // AddComponent 때(OnEnable)는 아직 화면이 없어 첫 갱신이 비었음
        }

        /// <summary>창 크기 캔버스를 모니터 앞에 만듦 (쪽 하나의 창)</summary>
        public RectTransform CreateWindowCanvas(string name, float depth, out GameObject content)
        {
            RectTransform rt = WorldUiKit.CreateCanvas(name, community.parent, WindowW, WindowH, out content);
            rt.localPosition = WindowCenter(depth);
            rt.localRotation = ScreenRotation;
            return rt;
        }

        /// <summary>쪽을 뒤에 붙이고 번호를 돌려줌. name은 작업 표시줄 탭 · 옆 버튼에 쓰임</summary>
        public int AddPage(IComputerPage p, string name)
        {
            pages.Add(p);
            names.Add(name);
            int index = pages.Count - 1;
            if (taskbar != null) taskbar.AddTab(name, () => GoTo(index));
            ApplyPage(true);
            return index;
        }

        /// <summary>이 캔버스 위에서 레이로 잡고 끌면 쪽을 넘김</summary>
        public void AddSwipe(RectTransform canvas) => canvas.gameObject.AddComponent<ScreenSwipeArea>().Swiped += OnSwiped;

        Button SideButton(float side, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick, out TMP_Text label)
        {
            Quaternion rot = ScreenRotation;
            RectTransform rt = WorldUiKit.CreateCanvas("Page_" + (side < 0 ? "Prev" : "Next"), chrome.transform, 160f, 420f, out GameObject content);
            rt.localPosition = ScreenCenter + rot * new Vector3(side, 0f, 0f) + rot * Vector3.back * 0.01f;
            rt.localRotation = rot;
            Button b = WorldUiKit.AddButton(content.transform, "", Vector2.zero, Vector2.one, font);
            b.onClick.AddListener(onClick);
            label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = 34f;
            return b;
        }

        void OnSwiped(float dx, float width)
        {
            int next = PagerRules.AfterDrag(page, pages.Count, dx, width);
            if (next != page) GoTo(next);
        }

        /// <summary>쪽으로 넘김 (0 = 커뮤니티, 1 = 슬라임마켓, 그 뒤는 AddPage 순서)</summary>
        public void GoTo(int target)
        {
            target = PagerRules.Clamp(target, pages.Count);
            if (target == page) return;
            slideDir = target > page ? 1 : -1;
            fromPage = page;
            page = target;
            slideT = 0f;
            Sfx.Play(SoundId.UiClick);
            Interaction.Haptics.BothHands(0.2f, 0.04f);
            ApplyPage(false);
        }

        protected override void Refresh()
        {
            bool home = IsHome;
            if (chrome != null) chrome.SetActive(home);
            if (home && page != CommunityPageIndex) { page = CommunityPageIndex; slideT = 1f; } // 집에 오면 커뮤니티부터 (Q5)
            if (taskbar != null && Context != null && Context.Session != null) taskbar.SetClock($"{MoneyText.Format(Context.Session.Wallet.Money)} · {Context.Session.Day}일차 밤");
            ApplyPage(true);
        }

        int clockMoney = int.MinValue;

        void Update()
        {
            // 작업 표시줄 「$돈 · N일차 밤」: 마켓에서 사면 돈이 바뀌므로 바뀔 때만 다시 씀
            if (taskbar != null && IsHome && Context != null && Context.Session != null && Context.Session.Wallet.Money != clockMoney)
            {
                clockMoney = Context.Session.Wallet.Money;
                taskbar.SetClock($"{MoneyText.Format(clockMoney)} · {Context.Session.Day}일차 밤");
            }
            if (slideT >= 1f) return;
            slideT = Mathf.Min(1f, slideT + Time.unscaledDeltaTime / SlideSeconds);
            ApplyPage(false);
        }

        // 지금 쪽은 가운데로 미끄러져 들어오고, 떠나는 쪽은 반대로 나가며 사라짐. 나머지는 숨김
        void ApplyPage(bool instant)
        {
            if (community == null || pages.Count == 0) return;
            if (instant) slideT = 1f;
            float t = 1f - (1f - slideT) * (1f - slideT);
            Vector3 right = ScreenRotation * Vector3.right;
            bool home = IsHome;

            for (int i = 0; i < pages.Count; i++)
            {
                if (i == page) pages[i].Show(t, right * (slideDir * SlideDistance * (1f - t)), home);
                else if (i == fromPage && t < 1f) pages[i].Show(1f - t, right * (-slideDir * SlideDistance * t), false);
                else pages[i].Show(0f, Vector3.zero, false);
            }

            SetSide(prevButton, prevLabel, page - 1, "◀\n", "");
            SetSide(nextButton, nextLabel, page + 1, "", "\n▶");
            if (taskbar != null) taskbar.SetCurrent(page);
        }

        void SetSide(Button b, TMP_Text label, int target, string before, string after)
        {
            if (b == null) return;
            bool has = target >= 0 && target < pages.Count;
            b.interactable = has;
            // 옆 버튼은 좁아서 세로로 한 글자씩 (예: "판\n매\n·\n진\n열")
            if (label != null) label.text = has ? before + string.Join("\n", names[target].Replace(" ", "").ToCharArray()) + after : "";
        }
    }
}
