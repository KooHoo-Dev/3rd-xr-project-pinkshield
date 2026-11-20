using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 최종 정산 판 (기획서 5-8): 재화 · 카드 가치 · 도감 · 점수 · 랭크 · 엔딩 + [계속하기] [타이틀로].
    /// [계속하기]는 무한 모드(무한모드_설계 J4): [타이틀로] 버튼을 실행 중에 복제해 만들어 씬을 다시 빌드하지 않습니다.
    /// 무한 모드를 끝내고 다시 오면 "N일 근무 끝!" 요약(평가 없음)과 [타이틀로]만 보입니다.
    /// </summary>
    public sealed class FinalBoard : PhaseBoard
    {
        protected override GamePhaseId Phase => GamePhaseId.Final;

        Button continueButton;
        RectTransform advanceRect;
        Vector2 advanceMin, advanceMax;

        // 끝은 "상장" 종이 (UI_개편_설계 C안 10번 판). 씬은 그대로 두고 실행할 때 칠함
        void Awake()
        {
            if (content != null) UiSkin.ApplyPanel(content.transform, UiSurface.Paper);
        }

        protected override void Start()
        {
            base.Start();
            Button advance = AdvanceButton;
            if (advance == null || continueButton != null) return;
            advanceRect = (RectTransform)advance.transform;
            advanceMin = advanceRect.anchorMin;
            advanceMax = advanceRect.anchorMax;
            GameObject go = Instantiate(advance.gameObject, advance.transform.parent, false);
            go.name = "Button_Continue";
            continueButton = go.GetComponent<Button>();
            UiSkin.StyleButton(continueButton, UiButtonKind.Primary); // 계속 일하기가 이 판의 주 버튼
            continueButton.onClick = new Button.ButtonClickedEvent(); // 복제된 [타이틀로] 연결을 떼어 냄
            continueButton.onClick.AddListener(OnContinue);
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "계속하기";
            Layout(true);
        }

        // 두 버튼을 나란히 (계속하기 왼쪽) 또는 [타이틀로]만 원래 자리
        void Layout(bool both)
        {
            if (advanceRect == null || continueButton == null) return;
            continueButton.gameObject.SetActive(both);
            float midY0 = advanceMin.y, midY1 = advanceMax.y;
            float w = advanceMax.x - advanceMin.x;
            if (both)
            {
                var c = (RectTransform)continueButton.transform;
                c.anchorMin = new Vector2(0.5f - w - 0.02f, midY0);
                c.anchorMax = new Vector2(0.5f - 0.02f, midY1);
                advanceRect.anchorMin = new Vector2(0.5f + 0.02f, midY0);
                advanceRect.anchorMax = new Vector2(0.5f + w + 0.02f, midY1);
            }
            else
            {
                advanceRect.anchorMin = advanceMin;
                advanceRect.anchorMax = advanceMax;
            }
        }

        void OnContinue()
        {
            if (Flow != null && Flow.CurrentPhase == GamePhaseId.Final) Flow.GetPhase<FinalPhase>(GamePhaseId.Final)?.Continue();
        }

        protected override void Redraw()
        {
            FinalResult r = Flow.GetPhase<FinalPhase>(GamePhaseId.Final)?.Result;
            EndlessState endless = Context.Session.Endless;
            // 가구 값 · 진열장 줄이 늘어도 판 밖으로 넘치지 않게 (원래 크기보다 커지지는 않음, 씬 다시 빌드 없이)
            if (body != null && !body.enableAutoSizing) { body.fontSizeMax = body.fontSize; body.fontSizeMin = 18f; body.enableAutoSizing = true; }
            Layout(!endless.Active);
            if (endless.Active)
            {
                if (heading != null) heading.text = $"{Context.Session.Day}일 근무 끝!";
                if (body != null) body.text = ResultTexts.EndlessFinal(endless, Context.Session);
                return;
            }
            bool success = r != null && r.Rank != null && r.Rank.isSuccess;
            if (heading != null) heading.text = $"{Context.Campaign.DayCount}일 알바 끝!";
            if (body != null) body.text = ResultTexts.Final(r, success ? Context.Score.successEnding : Context.Score.failEnding);
        }
    }
}
