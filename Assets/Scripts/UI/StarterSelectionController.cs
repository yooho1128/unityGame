using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.UI
{
    public class StarterSelectionController : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private List<ShadowData> starters = new List<ShadowData>();
        [SerializeField] private List<StarterCardView> cards = new List<StarterCardView>();

        private Action<ShadowData> _onSelected;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(Action<ShadowData> onSelected)
        {
            _onSelected = onSelected;
            if (root != null) root.SetActive(true);
            for (int i = 0; i < cards.Count; i++)
                cards[i].Bind(i < starters.Count ? starters[i] : null, Select);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            _onSelected = null;
        }

        private void Select(ShadowData starter)
        {
            var callback = _onSelected;
            Hide();
            callback?.Invoke(starter);
        }
    }
}
