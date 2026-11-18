using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 흐트러진 팩 (기획서 5-1). 손님 하나가 떨어뜨린 팩 묶음을 기억했다가, 모두 꽂이에 다시 꽂히면 완료합니다.
    /// 모양은 PackRack의 팩이 맡고, 이 오브젝트는 기록만 합니다.
    /// </summary>
    public sealed class MessyPackSpot : CleanupSpot
    {
        readonly List<RackPack> packs = new List<RackPack>();

        public void Init(List<RackPack> scattered)
        {
            packs.AddRange(scattered);
            foreach (RackPack p in packs) p.Returned += OnReturned;
            if (packs.Count == 0) Complete();
        }

        // 꽂힌 팩은 목록에서 뺌: 나중에 다른 손님이 같은 팩을 또 흐트러뜨려도 이 구역과는 상관없게
        void OnReturned(RackPack pack)
        {
            pack.Returned -= OnReturned;
            packs.Remove(pack);
            if (packs.Count == 0) Complete();
        }

        public override void Discard()
        {
            foreach (RackPack p in packs) if (p != null) p.Returned -= OnReturned;
            base.Discard(); // 꽂이는 CleanupDirector.EndDay가 한 번에 되돌림
        }
    }
}
