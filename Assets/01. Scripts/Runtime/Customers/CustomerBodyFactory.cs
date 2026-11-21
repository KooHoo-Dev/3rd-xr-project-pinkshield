using System;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 손님 몸 만들기: 오늘의 외형 뽑기 → 프리팹 → 애니메이터 · 키 맞추기 · 걷기(CustomerAgent) · 말풍선.
    /// CustomerDirector에서 떼어 냄 (큰_클래스_나누기_제안 2장). 도둑도 같은 공장으로 손님 외형을 빌려 씁니다(SpawnExtra).
    /// 만든 손님을 목록 · 줄에 넣는 일과 없애는 일은 부르는 쪽이 맡습니다.
    /// </summary>
    public sealed class CustomerBodyFactory
    {
        readonly Transform parent;
        readonly Func<Transform> spawnPoint;
        readonly Func<TMP_FontAsset> bubbleFont;
        readonly Func<bool> isPaused;
        CustomerRoster roster;
        CustomerPicker picker;
        IRandomSource random;

        /// <param name="parent">손님 오브젝트의 부모</param>
        /// <param name="spawnPoint">등장 지점 (매장 밖)</param>
        public CustomerBodyFactory(Transform parent, Func<Transform> spawnPoint, Func<TMP_FontAsset> bubbleFont, Func<bool> isPaused)
        {
            this.parent = parent;
            this.spawnPoint = spawnPoint;
            this.bubbleFont = bubbleFont;
            this.isPaused = isPaused;
        }

        /// <summary>오늘 뽑을 수 있는 외형이 준비됨 (영업 전이면 false).</summary>
        public bool Ready => picker != null;

        /// <summary>하루 시작: 로스터에서 외형 뽑기를 새로 준비합니다. 로스터가 비면 false (손님이 오지 않음).</summary>
        public bool BeginDay(CustomerRoster customerRoster, IRandomSource rng)
        {
            roster = customerRoster;
            random = rng;
            picker = null;
            if (roster == null || roster.entries.Count == 0) return false;

            var looks = new List<CustomerLook>(roster.entries.Count);
            foreach (CustomerRoster.Entry e in roster.entries) looks.Add(e.look);
            picker = new CustomerPicker(looks, roster.localWeight, roster.foreignWeight, roster.noRepeatCount, rng);
            return true;
        }

        /// <summary>손님 외형 하나를 밖 지점에 만들고 걷기 · 말풍선을 붙입니다. 오늘 외형을 아직 못 정했으면 null.</summary>
        public CustomerAgent Create(string prefix)
        {
            if (picker == null) return null;
            int index = picker.Next();
            if (index < 0) return null;
            CustomerRoster.Entry entry = roster.entries[index];
            if (entry.prefab == null) return null;

            Transform at = spawnPoint();
            GameObject go = UnityEngine.Object.Instantiate(entry.prefab, at.position, at.rotation, parent);
            go.name = $"{prefix}_{entry.prefab.name}";

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false; // 이동은 CustomerAgent가 함 (제자리 걷기 클립)
                animator.runtimeAnimatorController = roster.ControllerFor(entry.gender);
            }

            // 키 맞추기 (플레이 피드백: 손님이 너무 큼). 크기를 줄이면 보폭도 줄어드므로 걷는 속도도 같은 비율로.
            Vector2 range = roster.HeadHeightFor(entry.gender);
            float height = range.x + (float)random.NextDouble() * Mathf.Max(0f, range.y - range.x);
            float scale = HumanoidHeight.Fit(go, animator, height);

            var agent = go.AddComponent<CustomerAgent>();
            agent.Init(isPaused, animator, roster.walkSpeed * scale, roster.turnSpeedDegrees);
            agent.IsFemale = entry.gender == CustomerGender.Female;
            agent.MaskOffset = entry.maskOffset;
            agent.MaskScale = entry.maskScale;
            agent.Bubble = SpeechBubble.Create(go.transform, HumanoidHeight.Head(animator), bubbleFont());
            agent.Bubble.IsPaused = isPaused;
            return agent;
        }
    }
}
