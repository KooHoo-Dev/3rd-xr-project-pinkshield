using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Customers
{
    /// <summary>
    /// 손님이 계산대에 올린 물건 (가게 재고 · 판매 등록 카드). 바코드 스캐너가 찍으면 Scanned가 한 번 불립니다.
    /// 찍힌 뒤에는 물건 위에 이름 · 가격 표시가 뜹니다 (CheckoutCounter가 글자를 정함).
    /// 잡을 수 있고(XRGrabInteractable, 물리 없음), 놓으면 Released를 알린 뒤 봉투에 담기지 않았으면 잠시 뒤 제자리로 돌아갑니다
    /// (계산시스템_확장_설계 4장: 떨어뜨려도 못 줍는 일 없게, BarcodeScanner와 같은 방식).
    /// </summary>
    public sealed class ScannableItem : MonoBehaviour
    {
        [Tooltip("찍은 뒤 물건 위에 뜨는 이름 · 가격 (처음엔 숨김)")]
        [SerializeField] TMP_Text priceLabel;
        [Tooltip("놓은 뒤 제자리로 돌아가기까지 (초)")]
        [SerializeField] float returnDelay = 1.2f;

        XRGrabInteractable grab;
        Vector3 homePos;
        Quaternion homeRot;
        Coroutine returnRoutine;

        public bool IsScanned { get; private set; }
        public bool IsBagged { get; private set; }
        public Quaternion HomeRotation => homeRot;
        public event Action<ScannableItem> Scanned;
        /// <summary>
        /// 지금 찍을 수 있는지 (예: 일시정지 중이면 false). 받지 않을 때 IsScanned를 켜지 않아야
        /// 일시정지를 풀고 다시 찍을 수 있습니다 (Play 확인 C1: 일시정지 중 찍은 물건이 영영 안 찍히던 것).
        /// </summary>
        public Func<bool> CanScan { get; set; }
        /// <summary>손에서 놓음. 받는 쪽이 이 안에서 PutInBag을 부르면 제자리로 돌아가지 않습니다.</summary>
        public event Action<ScannableItem> Released;

        void Awake()
        {
            if (priceLabel != null) priceLabel.gameObject.SetActive(false);
            homePos = transform.position;
            homeRot = transform.rotation;
            grab = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            if (grab == null) return;
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
        }

        void OnReleased(SelectExitEventArgs _)
        {
            Released?.Invoke(this);
            if (!IsBagged && isActiveAndEnabled) returnRoutine = StartCoroutine(ReturnHome());
        }

        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(returnDelay);
            transform.SetPositionAndRotation(homePos, homeRot);
            returnRoutine = null;
        }

        /// <summary>바코드 스캐너가 호출. 처음 찍혔을 때만 true.</summary>
        public bool TryScan()
        {
            if (IsScanned || (CanScan != null && !CanScan())) return false;
            IsScanned = true;
            Scanned?.Invoke(this);
            return true;
        }

        public void ShowLabel(string text)
        {
            if (priceLabel == null) return;
            priceLabel.text = text;
            priceLabel.gameObject.SetActive(true);
        }

        /// <summary>
        /// 봉투 안 정해진 자리에 붙이고 더 잡을 수 없게 합니다 (물리로 쌓지 않음 → 튀거나 떨리지 않음).
        /// <paramref name="floor"/>: 물건 바닥이 닿을 월드 위치.
        /// </summary>
        public void PutInBag(Vector3 floor, Quaternion rotation)
        {
            IsBagged = true;
            if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
            transform.SetPositionAndRotation(floor + rotation * (Vector3.up * -LocalBottom()), rotation);
            if (grab != null) grab.enabled = false;
            if (priceLabel != null) priceLabel.gameObject.SetActive(false); // 봉투 위로 글자가 겹치지 않게
        }

        /// <summary>물건 높이 (봉투에 쌓을 때). 콜라이더가 없으면 1cm.</summary>
        public float Height
        {
            get
            {
                var box = GetComponent<BoxCollider>();
                return box != null ? box.size.y * transform.lossyScale.y : 0.01f;
            }
        }

        /// <summary>
        /// 물건 바닥이 <paramref name="surface"/>(월드 높이 기준점)에 닿게 놓고, 그 자리를 "제자리"로 삼습니다.
        /// 원점이 가운데인 카드 · 팩이 카운터 윗면에 반쯤 묻혀 안 보이던 문제(2026-11-18 기기 테스트 피드백) 때문에 씁니다.
        /// </summary>
        public void PlaceOn(Vector3 surface, Quaternion rotation, float gap = 0.002f)
        {
            Vector3 pos = surface + rotation * (Vector3.up * (gap - LocalBottom()));
            transform.SetPositionAndRotation(pos, rotation);
            homePos = pos;
            homeRot = rotation;
        }

        // 원점에서 물건 바닥까지 (아래가 음수). 원점이 가운데인 팩 · 카드도 바닥이 봉투 바닥에 닿게
        float LocalBottom()
        {
            var box = GetComponent<BoxCollider>();
            return box != null ? (box.center.y - box.size.y / 2f) * transform.lossyScale.y : 0f;
        }
    }
}
