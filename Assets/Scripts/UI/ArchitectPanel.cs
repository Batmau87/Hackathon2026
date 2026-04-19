using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

namespace HackathonJuego
{
    /// <summary>
    /// Panel del Arquitecto (Station 0).
    /// Muestra 3 toggles para elegir Dinero/Bomba por caja + botón Finalizar.
    /// Ponlo en un panel hijo del Canvas de gameplay.
    /// </summary>
    public class ArchitectPanel : MonoBehaviour
    {
        [Header("Toggles por Caja (true=Dinero, false=Bomba)")]
        public Toggle box0Toggle;
        public Toggle box1Toggle;
        public Toggle box2Toggle;

        [Header("Iconos / Sprites")]
        public Image box0Icon;
        public Image box1Icon;
        public Image box2Icon;
        public Sprite spriteDinero;
        public Sprite spriteBomba;

        [Header("Contadores")]
        public TextMeshProUGUI dineroCountText;
        public TextMeshProUGUI bombaCountText;

        [Header("Botón Finalizar")]
        public Button finalizarButton;

        private Gameplay _gameplay;

        private void OnEnable()
        {
            // Valores por defecto: todas Dinero
            if (box0Toggle != null) box0Toggle.isOn = true;
            if (box1Toggle != null) box1Toggle.isOn = true;
            if (box2Toggle != null) box2Toggle.isOn = true;

            box0Toggle?.onValueChanged.AddListener(_ => RefreshUI());
            box1Toggle?.onValueChanged.AddListener(_ => RefreshUI());
            box2Toggle?.onValueChanged.AddListener(_ => RefreshUI());

            if (finalizarButton != null)
                finalizarButton.onClick.AddListener(OnFinalizarClicked);

            RefreshUI();
        }

        private void OnDisable()
        {
            box0Toggle?.onValueChanged.RemoveAllListeners();
            box1Toggle?.onValueChanged.RemoveAllListeners();
            box2Toggle?.onValueChanged.RemoveAllListeners();

            if (finalizarButton != null)
                finalizarButton.onClick.RemoveListener(OnFinalizarClicked);
        }

        private void RefreshUI()
        {
            int dinero = 0, bombas = 0;

            UpdateIcon(box0Toggle, box0Icon, ref dinero, ref bombas);
            UpdateIcon(box1Toggle, box1Icon, ref dinero, ref bombas);
            UpdateIcon(box2Toggle, box2Icon, ref dinero, ref bombas);

            if (dineroCountText != null) dineroCountText.text = $"💰 x{dinero}";
            if (bombaCountText != null) bombaCountText.text = $"💣 x{bombas}";
        }

        private void UpdateIcon(Toggle toggle, Image icon, ref int dinero, ref int bombas)
        {
            if (toggle == null) return;
            bool isDinero = toggle.isOn;
            if (isDinero) dinero++; else bombas++;

            if (icon != null)
                icon.sprite = isDinero ? spriteDinero : spriteBomba;
        }

        private int GetBoxContent(Toggle toggle)
        {
            // 1 = Dinero, 2 = Bomba
            return (toggle != null && toggle.isOn) ? 1 : 2;
        }

        private void OnFinalizarClicked()
        {
            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();

            if (_gameplay == null) return;

            int b0 = GetBoxContent(box0Toggle);
            int b1 = GetBoxContent(box1Toggle);
            int b2 = GetBoxContent(box2Toggle);

            _gameplay.RPC_ConfigurarCajas(b0, b1, b2);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUIClick();
        }
    }
}
