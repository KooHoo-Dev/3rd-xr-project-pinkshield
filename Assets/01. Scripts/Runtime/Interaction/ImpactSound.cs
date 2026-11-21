using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Interaction
{
    /// <summary>
    /// 물리로 움직이는 손 물건이 바닥 · 책상에 세게 부딪힐 때 소리 (11-21 피드백 S5). GrabSound가 붙입니다.
    /// 손에 든 동안 · 살짝 내려놓을 때(0.6m/s 미만)는 조용하고, 튕기며 여러 번 닿아도 0.15초에 한 번만.
    /// </summary>
    public sealed class ImpactSound : MonoBehaviour
    {
        GrabSound owner;
        XRBaseInteractable interactable;
        float nextAllowed;

        public void Init(GrabSound sound)
        {
            owner = sound;
            interactable = GetComponent<XRBaseInteractable>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (owner == null || Time.time < nextAllowed) return;
            if (interactable != null && interactable.isSelected) return;
            float volume = GrabSoundRules.ImpactVolume(collision.relativeVelocity.magnitude);
            if (volume <= 0f) return;
            nextAllowed = Time.time + GrabSoundRules.ImpactGap;
            Sfx.PlayScaled(GrabSoundRules.FallSound(owner.Kind), collision.GetContact(0).point, volume);
        }
    }
}
