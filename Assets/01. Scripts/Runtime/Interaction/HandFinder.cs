using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>왼손·오른손 컨트롤러 인터랙터 찾기. 손 하나에 인터랙터가 여럿이면(근접·원거리·텔레포트) 근접·원거리 겸용을 우선합니다.</summary>
    public static class HandFinder
    {
        static readonly List<IXRInteractor> buffer = new List<IXRInteractor>();

        public static XRBaseInputInteractor Find(InteractorHandedness hand, XRInteractionManager manager = null)
        {
            if (manager == null) manager = Object.FindAnyObjectByType<XRInteractionManager>();
            if (manager == null) return null;

            buffer.Clear();
            manager.GetRegisteredInteractors(buffer);
            XRBaseInputInteractor fallback = null;
            foreach (IXRInteractor i in buffer)
            {
                if (i.handedness != hand || i is not XRBaseInputInteractor input) continue;
                if (input is NearFarInteractor) return input;
                fallback ??= input;
            }
            return fallback;
        }
    }
}
