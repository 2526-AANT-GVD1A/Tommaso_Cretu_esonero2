using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcadeKart.Gameplay;

namespace ArcadeKart.Menu
{
    // Genera dinamicamente i bottoni del menu livelli: un bottone per ogni
    // voce del LevelManager, clonato dal template (l'oggetto Menuext, che
    // resta disattivato). Alla riapertura del menu i bottoni vengono
    // rigenerati. Al click: carica il livello e chiude il menu (l'oggetto
    // Menu si disattiva -> MenuControls riattiva i controlli del kart).
    public class LevelMenu : MonoBehaviour
    {
        [Header("Riferimenti")]
        [SerializeField, Tooltip("LevelManager che riceve la selezione del livello.")]
        private LevelManager gestore;

        [SerializeField, Tooltip("Bottone template da clonare per ogni livello. Viene tenuto disattivato.")]
        private GameObject templateBottone;

        [SerializeField, Tooltip("Distanza verticale (pixel) fra un bottone e il successivo.")]
        private float spaziaturaBottoni = 40f;

        private readonly List<GameObject> bottoniGenerati = new List<GameObject>();

        private void OnEnable()
        {
            RigeneraBottoni();
        }

        private void RigeneraBottoni()
        {
            foreach (GameObject bottone in bottoniGenerati)
            {
                if (bottone != null)
                    Destroy(bottone);
            }
            bottoniGenerati.Clear();

            if (gestore == null || templateBottone == null)
            {
                Debug.LogWarning("[LevelMenu] Gestore o template bottone non assegnati.", this);
                return;
            }

            if (templateBottone.activeSelf)
                templateBottone.SetActive(false);

            RectTransform templateRect = templateBottone.transform as RectTransform;
            Vector2 posizioneBase = templateRect != null ? templateRect.anchoredPosition : Vector2.zero;

            IReadOnlyList<VoceLivello> livelli = gestore.Livelli;
            int count = livelli.Count;

            for (int i = 0; i < count; i++)
            {
                int indice = i;
                VoceLivello voce = livelli[i];

                GameObject bottone = Instantiate(templateBottone, templateBottone.transform.parent);
                bottone.name = string.IsNullOrEmpty(voce.nome)
                    ? $"BottoneLivello{indice + 1}"
                    : $"Bottone_{voce.nome}";
                bottone.SetActive(true);

                RectTransform rect = bottone.transform as RectTransform;
                if (rect != null)
                {
                    // Centra verticalmente la pila di bottoni attorno alla posizione del template.
                    Vector2 posizione = posizioneBase;
                    posizione.y += spaziaturaBottoni * ((count - 1) * 0.5f - indice);
                    rect.anchoredPosition = posizione;
                }

                TextMeshProUGUI etichetta = bottone.GetComponentInChildren<TextMeshProUGUI>();
                if (etichetta != null)
                    etichetta.text = string.IsNullOrEmpty(voce.nome) ? $"Livello {indice + 1}" : voce.nome;

                Button componenteBottone = bottone.GetComponent<Button>();
                if (componenteBottone != null)
                    componenteBottone.onClick.AddListener(() => SelezionaLivello(indice));

                bottoniGenerati.Add(bottone);
            }
        }

        private void SelezionaLivello(int indice)
        {
            gestore.CaricaLivello(indice);
            gameObject.SetActive(false);
        }
    }
}
