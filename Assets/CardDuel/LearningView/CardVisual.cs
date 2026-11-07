using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardDuel.Learning
{
    public sealed class CardVisual : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        public Image face;
        public Image effectBadge;
        public Text effectText;
        public Text originalText;
        public Outline selectionOutline;
        public Button button;

        public int CardId { get; set; }

        Action clicked;
        Action showDetails;
        Action hideDetails;

        bool down;
        bool held;
        bool moved;
        float pressedAt;
        Vector2 pressedPosition;

        public void Bind(
            Sprite sprite,
            string badge,
            Color badgeColor,
            string original,
            bool selected,
            bool selectable,
            Action click,
            Action show,
            Action hide)
        {
            face.sprite = sprite;
            face.preserveAspect = true;

            effectBadge.gameObject.SetActive(!string.IsNullOrEmpty(badge));
            effectBadge.color = badgeColor;
            effectText.text = badge;

            originalText.text = original;
            originalText.transform.parent.gameObject.SetActive(
                !string.IsNullOrEmpty(original));
            selectionOutline.enabled = selected;
            button.interactable = selectable;

            clicked = click;
            showDetails = show;
            hideDetails = hide;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            down = true;
            held = false;
            moved = false;
            pressedAt = Time.unscaledTime;
            pressedPosition = eventData.position;
        }

        void Update()
        {
            if (!down)
                return;

            if (Mouse.current != null &&
                Vector2.Distance(
                    Mouse.current.position.ReadValue(),
                    pressedPosition) > 12)
            {
                moved = true;
            }

            if (!held && !moved &&
                Time.unscaledTime - pressedAt >= 0.45f)
            {
                held = true;
                showDetails?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            down = false;
            hideDetails?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            down = false;
            moved = true;
            hideDetails?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                !held && !moved && button.interactable)
            {
                clicked?.Invoke();
            }
        }

        void OnDisable()
        {
            down = false;
            hideDetails?.Invoke();
        }
    }
}