using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UI.Guide;
using UI.Guide.GuideEntries;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignalForge
{
    // An "SignalForge" entry in the in-game guide (G) explaining every part, in French or English.
    // The page is a copy of a vanilla guide page with its content replaced by one text block, so it
    // keeps the guide's font and colours; the guide instantiates it like any other page.
    internal static class Guide
    {
        private const uint EntryId = 48001;

        private static readonly System.Reflection.FieldInfo ButtonPrefab = AccessTools.Field(typeof(GuideCanvas), "entrySelectButtonPrefab");
        private static readonly System.Reflection.FieldInfo ButtonContainer = AccessTools.Field(typeof(GuideCanvas), "entriesButtonContainer");
        private static readonly System.Reflection.FieldInfo Buttons = AccessTools.Field(typeof(GuideCanvas), "_buttons");
        private static readonly System.Reflection.FieldInfo Content = AccessTools.Field(typeof(GuideEntryButton), "_content");
        private static readonly System.Reflection.FieldInfo Available = AccessTools.Field(typeof(GuideEntryButton), "_isAvailable");

        private static GuideEntryContent _page;

        public static void AfterInstantiateButtons(GuideCanvas __instance)
        {
            try
            {
                List<GuideEntryButton> buttons = Buttons.GetValue(__instance) as List<GuideEntryButton>;
                if (buttons == null) return;
                foreach (GuideEntryButton existing in buttons)
                    if (existing != null && existing.PageContent != null && existing.PageContent.EntryId == EntryId) return;

                GuideEntryContent page = Page(buttons);
                GuideEntryButton prefab = ButtonPrefab.GetValue(__instance) as GuideEntryButton;
                Transform container = ButtonContainer.GetValue(__instance) as Transform;
                if (page == null || prefab == null || container == null) return;

                GuideEntryButton button = Object.Instantiate(prefab, container);
                Content.SetValue(button, page);
                Available.SetValue(button, true);
                foreach (LocalizeStringEvent localized in button.GetComponentsInChildren<LocalizeStringEvent>(true))
                    localized.enabled = false;
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                    label.text = "SignalForge";
                buttons.Add(button);
            }
            catch (Exception e)
            {
                Log.Once("Guide", e);
            }
        }

        private static GuideEntryContent Page(List<GuideEntryButton> buttons)
        {
            if (_page != null) return _page;
            GuideEntryContent source = null;
            foreach (GuideEntryButton b in buttons)
                if (b != null && b.PageContent != null) { source = b.PageContent; break; }
            if (source == null)
            {
                MelonLogger.Warning("[SignalForge] guide: no vanilla page to copy");
                return null;
            }

            GameObject holder = new GameObject("SF_GuideTemplates");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            GuideEntryContent page = Object.Instantiate(source, holder.transform);
            page.name = "SF_GuidePage";
            page.entryId = EntryId;

            // Take the font of the page's longest text, then clear the page.
            TMP_Text style = null;
            foreach (TMP_Text t in page.GetComponentsInChildren<TMP_Text>(true))
                if (style == null || (t.text ?? "").Length > (style.text ?? "").Length) style = t;
            TMP_FontAsset font = style != null ? style.font : null;
            float size = style != null ? style.fontSize : 24f;
            Color color = style != null ? style.color : Color.white;

            List<Transform> children = new List<Transform>();
            foreach (Transform child in page.transform) children.Add(child);
            foreach (Transform child in children) Object.DestroyImmediate(child.gameObject);
            foreach (LayoutGroup layout in page.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);

            VerticalLayoutGroup column = page.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(30, 30, 20, 30);
            column.childControlHeight = true;
            column.childControlWidth = true;
            column.childForceExpandHeight = false;
            column.childForceExpandWidth = true;
            ContentSizeFitter fit = page.GetComponent<ContentSizeFitter>() ?? page.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(page.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.text = Lang.French ? French : English;

            _page = page;
            return page;
        }

        private const string H = "<size=130%><b><color=#4FC3FF>";
        private const string E = "</color></b></size>";

        private static readonly string French =
            H + "SignalForge" + E + "\n" +
            "Toutes les pièces sont dans l'onglet SignalForge du menu de construction. " +
            "Pour la logique, un signal de 0,5 ou plus compte comme 1.\n\n" +
            H + "Lampe à écran" + E + "\n" +
            "Le hublot s'allume selon le signal branché sur la prise au bout, sans courant. " +
            "Appuie sur F pour écrire ton texte sur l'écran ; sans texte, l'écran affiche la couleur.\n" +
            "Couleur : sous 0,25 rose, sous 0,5 vert, sous 0,75 jaune, au-dessus rouge ; à 0, éteinte.\n" +
            "Le texte sert aussi de canal : un Signal Receiver réglé sur ce nom reçoit la valeur de la lampe.\n\n" +
            H + "Hublot lumineux" + E + "\n" +
            "Le hublot seul, mêmes couleurs, prise sur le côté.\n\n" +
            H + "Portes logiques" + E + "\n" +
            "Deux entrées A et B, une sortie qui vaut 1 ou 0.\n" +
            "• Porte ET : 1 si A et B sont à 1.\n" +
            "• Porte OU : 1 si A ou B est à 1.\n" +
            "• Porte XOR : 1 si une seule des deux est à 1.\n" +
            "• Porte logique : F change de fonction — AND, OR, XOR, NAND (contraire de AND), NOR (contraire de OR), " +
            "XNOR (1 si A et B sont pareils), NOT (contraire de A), SR (A allume et garde allumé, B éteint), " +
            "T (chaque appui sur A inverse la sortie, B remet à 0), BUF (recopie A).\n\n" +
            H + "Bouton poussoir NO" + E + "\n" +
            "Envoie 1 tant que tu appuies avec F (au moins 0,4 s). Avec une porte en mode T, il devient un interrupteur.\n\n" +
            H + "Bouton poussoir NF" + E + "\n" +
            "Contact normalement fermé, dans les deux sens : au repos le signal passe d'une prise à l'autre, " +
            "pendant l'appui il est coupé. Idéal comme arrêt d'urgence.\n\n" +
            H + "Interrupteur" + E + "\n" +
            "F bascule entre 0 et 1 ; il garde sa position.\n\n" +
            H + "Sectionneur" + E + "\n" +
            "Relie deux réseaux électriques (jusqu'à 100 kW). Signal de 0,5 ou plus sur l'un des côtés : " +
            "le courant passe, poignée ouverte, voyant vert. En dessous : coupé, voyant rouge. Sans câble de signal, le courant passe.\n\n" +
            H + "Porte à signal" + E + "\n" +
            "Signal de 0,5 ou plus sur la prise du cadre : la porte s'ouvre ; en dessous, elle se ferme. " +
            "Sans câble, elle s'ouvre à la main.";

        private static readonly string English =
            H + "SignalForge" + E + "\n" +
            "Every part is in the SignalForge tab of the build menu. " +
            "For logic, a signal of 0.5 or more counts as 1.\n\n" +
            H + "Screen Lamp" + E + "\n" +
            "The window lights up from the signal plugged into the socket at the end, with no power. " +
            "Press F to write your text on the screen; with no text, the screen shows the colour.\n" +
            "Colour: below 0.25 pink, below 0.5 green, below 0.75 yellow, above that red; at 0, off.\n" +
            "The text is also a channel: a Signal Receiver set to that name gets the lamp's value.\n\n" +
            H + "Light Porthole" + E + "\n" +
            "Just the window, same colours, socket on the side.\n\n" +
            H + "Logic gates" + E + "\n" +
            "Two inputs A and B, one output that is 1 or 0.\n" +
            "• AND Gate: 1 when A and B are both 1.\n" +
            "• OR Gate: 1 when A or B is 1.\n" +
            "• XOR Gate: 1 when exactly one of them is 1.\n" +
            "• Logic Gate: F cycles the function — AND, OR, XOR, NAND (opposite of AND), NOR (opposite of OR), " +
            "XNOR (1 when A and B match), NOT (opposite of A), SR (A turns on and keeps it on, B turns off), " +
            "T (each press on A flips the output, B resets to 0), BUF (copies A).\n\n" +
            H + "Push Button NO" + E + "\n" +
            "Sends 1 as long as you press it with F (at least 0.4 s). With a gate in T mode it becomes an on/off switch.\n\n" +
            H + "Push Button NC" + E + "\n" +
            "Normally closed contact, both ways: at rest the signal passes from one socket to the other, " +
            "while pressed it is cut. Ideal as an emergency stop.\n\n" +
            H + "Toggle Switch" + E + "\n" +
            "F flips between 0 and 1; it keeps its position.\n\n" +
            H + "Circuit Breaker" + E + "\n" +
            "Joins two power networks (up to 100 kW). Signal of 0.5 or more on either side: power flows, " +
            "handle open, green light. Below that: cut, red light. With no signal cable, power flows.\n\n" +
            H + "Signal Door" + E + "\n" +
            "Signal of 0.5 or more on the frame socket: the door opens; below that, it closes. " +
            "With no cable it opens by hand.";
    }
}
