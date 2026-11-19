using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Cards
{
    public enum CustomerGender { Male, Female }

    /// <summary>나이대. 손님 목소리 칸을 고르는 데만 씁니다 (CustomerVoices). 외형 분류 창의 '나이' 보기에서 정합니다.</summary>
    public enum CustomerAge { Adult, Young }

    /// <summary>외형 분류. 손님 비율(아시아계 대부분 · 가끔 외국인)을 정하는 데만 씁니다.</summary>
    public enum CustomerLook { Local, Foreign }

    /// <summary>
    /// 손님 외형 목록과 등장 비율 · 이동 설정 (타이틀연출_손님_설계 3장).
    /// 목록은 Tools > CardShop > Customers > Build Customer Roster 가 Toon City People 변형 프리팹으로 채우고,
    /// 분류(Local/Foreign)는 Tools > CardShop > Customers > Sort Customer Looks 창에서 눈으로 보고 정합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Customer Roster", fileName = "CustomerRoster")]
    public class CustomerRoster : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public GameObject prefab;
            public CustomerGender gender;
            public CustomerLook look = CustomerLook.Local;
            public CustomerAge age = CustomerAge.Adult;
        }

        public List<Entry> entries = new List<Entry>();

        [Header("등장 비율 (가중치)")]
        [Min(0)] public int localWeight = 85;
        [Min(0)] public int foreignWeight = 15;
        [Tooltip("최근 이만큼의 손님과 같은 외형은 다시 뽑지 않음 (후보가 모자라면 무시)")]
        [Min(0)] public int noRepeatCount = 3;

        [Header("애니메이션 (Kevin Iglesias, CustomerAnimatorBuilder가 만듦)")]
        public RuntimeAnimatorController maleController;
        public RuntimeAnimatorController femaleController;

        [Header("키 (플레이어 눈높이 1.6m 기준, 머리 뼈 높이 m)")]
        [Tooltip("남성 손님의 머리 뼈 높이 범위. 눈은 이보다 약 7cm 위")]
        // VR 실기기(서서 플레이) 피드백: 1.45~1.60 / 1.38~1.52는 작아 보여 올림
        public Vector2 maleHeadHeight = new Vector2(1.58f, 1.72f);
        [Tooltip("여성 손님의 머리 뼈 높이 범위")]
        public Vector2 femaleHeadHeight = new Vector2(1.50f, 1.64f);

        [Header("이동 · 행동")]
        [Tooltip("걷는 속도(m/s). Walk01_Forward 발걸음과 맞춰 조정")]
        public float walkSpeed = 1.2f;
        public float turnSpeedDegrees = 360f;
        [Tooltip("진열대 앞에서 구경하는 시간(초) 범위")]
        public Vector2 browseSeconds = new Vector2(3f, 6f);

        public RuntimeAnimatorController ControllerFor(CustomerGender g) =>
            g == CustomerGender.Female ? femaleController : maleController;

        public Vector2 HeadHeightFor(CustomerGender g) => g == CustomerGender.Female ? femaleHeadHeight : maleHeadHeight;
    }
}
