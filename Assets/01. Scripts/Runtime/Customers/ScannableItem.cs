using System;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 손님이 계산대에 올린 물건 (가게 재고 카드팩 또는 판매 등록 카드). 바코드 스캐너가 찍으면 Scanned가 한 번 불립니다.
    /// 찍힌 뒤에는 물건 위에 이름 · 가격 표시가 뜹니다 (CustomerDirector가 글자를 정함).
    /// </summary>
    public sealed class ScannableItem : MonoBehaviour
    {
        [Tooltip("찍은 뒤 물건 위에 뜨는 이름 · 가격 (처음엔 숨김)")]
        [SerializeField] TMP_Text priceLabel;

        public bool IsScanned { get; private set; }
        public event Action<ScannableItem> Scanned;

        void Awake()
        {
            if (priceLabel != null) priceLabel.gameObject.SetActive(false);
        }

        /// <summary>바코드 스캐너가 호출. 처음 찍혔을 때만 true.</summary>
        public bool TryScan()
        {
            if (IsScanned) return false;
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
    }
}
