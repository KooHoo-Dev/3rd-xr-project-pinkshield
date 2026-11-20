using System.Text;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 실제 데이터 에셋으로 7일 한 판을 여러 번 돌려 점수 분포를 봅니다 (경제시스템_설계 E5).
    /// 결과는 테스트 로그(Debug.Log)에 표로 나오고, 랭크 기준을 정할 때 씁니다. 숫자 자체를 단정하지는 않고
    /// "돈 복사가 안 되는지 · 가구를 사도 크게 손해 보지 않는지"만 확인합니다.
    /// </summary>
    public class EconomySimulationTests
    {
        const int Runs = 400;

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, path);
            return a;
        }

        static EconomySimulator Make() => new EconomySimulator(
            Load<EconomyConfig>("Assets/02. Data/Configs/EconomyConfig.asset"),
            Load<RarityTable>("Assets/02. Data/Configs/RarityTable.asset"),
            Load<PackOddsConfig>("Assets/02. Data/Configs/PackOddsConfig.asset"),
            Load<CardDatabase>("Assets/02. Data/CardDatabase.asset"),
            Load<CampaignConfig>("Assets/02. Data/Configs/CampaignConfig.asset"),
            Load<ScoreConfig>("Assets/02. Data/Configs/ScoreConfig.asset"),
            UpgradeCatalog.Load());

        static EconomySimulator.Policy NoFurniture() => new EconomySimulator.Policy("가구 안 삼");

        static EconomySimulator.Policy AllFurniture() => new EconomySimulator.Policy("가구 전부")
            .Buy(1, UpgradeKind.Binder).Buy(2, UpgradeKind.Showcase).Buy(2, UpgradeKind.Bed)
            .Buy(3, UpgradeKind.Binder).Buy(3, UpgradeKind.Poster).Buy(3, UpgradeKind.Rug)
            .Buy(4, UpgradeKind.Showcase).Buy(4, UpgradeKind.Lamp).Buy(4, UpgradeKind.Figure).Buy(4, UpgradeKind.AirCon)
            .Buy(5, UpgradeKind.Computer).Buy(5, UpgradeKind.Bed).Buy(6, UpgradeKind.Computer);

        [Test]
        public void ScoreDistribution_ByPolicy_Logged()
        {
            EconomySimulator sim = Make();
            var sb = new StringBuilder("[EconomySimulation] 7일 × " + Runs + "판 (p25 / p50 / p75 / p90, 가구에 쓴 돈 평균)\n");
            var policies = new[]
            {
                NoFurniture(),
                new EconomySimulator.Policy("바인더 2 · 3").Buy(1, UpgradeKind.Binder).Buy(3, UpgradeKind.Binder),
                new EconomySimulator.Policy("진열장 2 · 3").Buy(1, UpgradeKind.Showcase).Buy(3, UpgradeKind.Showcase),
                new EconomySimulator.Policy("침대 2 · 3").Buy(1, UpgradeKind.Bed).Buy(3, UpgradeKind.Bed),
                new EconomySimulator.Policy("꾸미기 5종").Buy(1, UpgradeKind.Poster).Buy(1, UpgradeKind.Rug).Buy(2, UpgradeKind.Lamp)
                    .Buy(2, UpgradeKind.Figure).Buy(3, UpgradeKind.AirCon),
                AllFurniture(),
            };
            foreach (EconomySimulator.Policy p in policies)
            {
                EconomySimulator.Result r = sim.Run(p, Runs, 1000);
                double spent = 0; foreach (int x in r.FurnitureSpent) spent += x;
                sb.AppendLine($"{p.Name,-10} {r.Percentile(0.25f),5} / {r.Percentile(0.5f),5} / {r.Percentile(0.75f),5} / {r.Percentile(0.9f),5}   가구 {spent / Runs:0}");
            }
            Debug.Log(sb.ToString());
        }

        [Test]
        public void NoMoneyCopying_MedianScoreStaysBounded()
        {
            EconomySimulator.Result r = Make().Run(NoFurniture(), Runs, 7);
            Assert.Less(r.Percentile(0.5f), 10000, "상자 → 즉시 판매 돈 복사가 막혀 점수가 수만으로 튀지 않음 (DEC-041)");
            Assert.Greater(r.Percentile(0.5f), 300, "보통 플레이로도 점수가 쌓임");
        }

        // DEC-040 14-2 "3일차 안에 사면 손해 안 봄": 분야 하나를 끝까지 사면 안 산 사람과 비슷 (가운데값 95% 이상).
        // 가구를 전부 사는 것은 늦게 산 가구가 7일 안에 본전을 못 뽑아 조금 손해 (11-21 시뮬레이션 약 −21%, 무한 모드에서 거둠)
        [TestCase("바인더", UpgradeKind.Binder)]
        [TestCase("진열장", UpgradeKind.Showcase)]
        public void OneTrackByDay3_NotBelowNoFurniture(string name, UpgradeKind kind)
        {
            EconomySimulator sim = Make();
            int none = sim.Run(NoFurniture(), Runs, 11).Percentile(0.5f);
            int track = sim.Run(new EconomySimulator.Policy(name).Buy(1, kind).Buy(3, kind), Runs, 11).Percentile(0.5f);
            Assert.GreaterOrEqual(track, none * 0.95f, $"{name} 2 · 3단계: 안 삼 {none}, 삼 {track}");
        }

        [Test]
        public void DecorSet_NotBelowNoFurniture()
        {
            EconomySimulator sim = Make();
            int none = sim.Run(NoFurniture(), Runs, 11).Percentile(0.5f);
            int decor = sim.Run(new EconomySimulator.Policy("꾸미기").Buy(1, UpgradeKind.Poster).Buy(1, UpgradeKind.Rug)
                .Buy(2, UpgradeKind.Lamp).Buy(2, UpgradeKind.Figure).Buy(3, UpgradeKind.AirCon), Runs, 11).Percentile(0.5f);
            Assert.GreaterOrEqual(decor, none * 0.95f, $"꾸미기 5종: 안 삼 {none}, 삼 {decor}");
        }
    }
}
