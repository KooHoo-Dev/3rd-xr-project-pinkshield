using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>손 물건의 소리 종류 (잡을 때 · 떨어질 때 같은 재질 소리)</summary>
    public enum GrabKind { Item, Pack, Box, Trash, Card }

    /// <summary>
    /// 잡기 · 떨어짐 소리 고르기 (11-21 피드백 S3 · S5). 물건이 무엇인지는 부르는 쪽(GrabSound)이 컴포넌트로 알려 주고,
    /// 모르는 물건(계산대 손님 물건처럼 종류 정보가 없는 것)은 이름으로 짐작합니다. 순수 C#이라 GrabSoundRulesTests로 고정합니다.
    /// </summary>
    public static class GrabSoundRules
    {
        /// <param name="isTrash">쓰레기(TrashSpot)</param>
        /// <param name="isPack">카드팩(PackView · 꽂이 팩)</param>
        /// <param name="isBox">상자(택배 · 카드 상자)</param>
        /// <param name="name">오브젝트 이름 — 위 셋이 모두 아니면 이름 속 낱말로 (예: CustomerItem_Box)</param>
        public static GrabKind Classify(bool isTrash, bool isPack, bool isBox, string name)
        {
            if (isTrash) return GrabKind.Trash;
            if (isPack) return GrabKind.Pack;
            if (isBox) return GrabKind.Box;
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("box") || n.Contains("상자")) return GrabKind.Box;
            if (n.Contains("pack") || n.Contains("팩")) return GrabKind.Pack;
            if (n.Contains("card") || n.Contains("카드")) return GrabKind.Card;
            // 쓰레기는 TrashSpot으로 알 수 있어 이름은 "trash"만 봄 ("can"은 scanner, "paper"는 PaperBag에도 들어 있음)
            if (n.Contains("trash")) return GrabKind.Trash;
            return GrabKind.Item;
        }

        public static SoundId PickSound(GrabKind kind) => kind switch
        {
            GrabKind.Pack => SoundId.PackPick,
            GrabKind.Box => SoundId.BoxPick,
            GrabKind.Trash => SoundId.TrashPick,
            GrabKind.Card => SoundId.CardPick,
            _ => SoundId.ItemPick,
        };

        /// <summary>바닥 · 책상에 떨어질 때. 상자 · 물건은 둔탁한 툭(BoxPick), 팩 · 카드는 종이 소리(PackFall)</summary>
        public static SoundId FallSound(GrabKind kind) => kind switch
        {
            GrabKind.Trash => SoundId.TrashFall,
            GrabKind.Pack or GrabKind.Card => SoundId.PackFall,
            _ => SoundId.BoxPick,
        };

        /// <summary>떨어짐 소리를 낼 만큼 세게 부딪혔나 (m/s). 살짝 내려놓을 때는 조용히</summary>
        public const float MinImpactSpeed = 0.6f;
        /// <summary>같은 물건이 튕기며 여러 번 부딪힐 때 소리 간격 (초)</summary>
        public const float ImpactGap = 0.15f;
        /// <summary>HeldItem이 놓인 뒤 이 높이(m) 넘게 떨어졌을 때만 떨어짐 소리</summary>
        public const float MinDropHeight = 0.15f;

        /// <summary>부딪힌 세기로 소리 크기 (0.6m/s = 0.35 ~ 3m/s 이상 = 1)</summary>
        public static float ImpactVolume(float speed)
        {
            if (speed < MinImpactSpeed) return 0f;
            float t = (speed - MinImpactSpeed) / (3f - MinImpactSpeed);
            return 0.35f + 0.65f * (t > 1f ? 1f : t);
        }
    }
}
