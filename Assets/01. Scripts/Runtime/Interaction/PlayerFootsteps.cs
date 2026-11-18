using CardShop.Cards;
using CardShop.Presentation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CardShop.Interaction
{
    /// <summary>
    /// 플레이어 걷는 소리 (11-19 필수 사운드 목록): 스틱 이동으로 리그가 일정 거리를 갈 때마다 발소리 하나.
    /// 머리만 움직이는(제자리에서 고개 돌리기 · 몸 숙이기) 것은 세지 않도록 리그(XR Origin) 이동만 봅니다.
    /// 순간 이동(구역 이동 · 경계 되돌리기)처럼 한 번에 크게 움직이면 소리를 내지 않습니다.
    /// 씬이 열리면 XR Origin에 스스로 붙습니다. 소리는 SoundBank의 Footstep 칸 (비면 조용).
    /// </summary>
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [Tooltip("발소리 간격 (m)")]
        [SerializeField] float stride = 0.65f;
        [Tooltip("한 프레임에 이보다 많이 움직이면 순간 이동으로 보고 무시 (m)")]
        [SerializeField] float teleportJump = 0.5f;

        Vector3 last;
        float walked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            XROrigin rig = FindAnyObjectByType<XROrigin>();
            if (rig != null && rig.GetComponent<PlayerFootsteps>() == null) rig.gameObject.AddComponent<PlayerFootsteps>();
        }

        void OnEnable() => last = transform.position;

        void Update()
        {
            Vector3 now = transform.position;
            Vector3 delta = now - last;
            delta.y = 0f;
            last = now;
            float d = delta.magnitude;
            if (d > teleportJump) { walked = 0f; return; }
            walked += d;
            if (walked < stride) return;
            walked = 0f;
            Sfx.Play(SoundId.Footstep, now);
        }
    }
}
