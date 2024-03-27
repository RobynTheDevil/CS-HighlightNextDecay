using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using SecretHistories;
using SecretHistories.UI;
using SecretHistories.Manifestations;
using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Abstract;
using HarmonyLib;

public class HighlightNextDecay : MonoBehaviour
{
    //public static PatchTracker showHighlight {get; private set;}
    public static ValueTracker<bool> cardHighlight {get; private set;}
    public static ValueTracker<bool> verbHighlight {get; private set;}
    public static Color primaryColor = UIStyle.brightPink;
    public static Color secondaryColor = (Color) new Color32((byte) 179, (byte) 30, (byte) 144, byte.MaxValue);
    public static Color cardColor {get; private set;}
    public static Color verbColor {get; private set;}

    public static void Initialise() {
        //Harmony.DEBUG = true;
        //Patch.harmony = new Harmony("robynthedevil.highlightnextdecay");
		new GameObject().AddComponent<HighlightNextDecay>();
        NoonUtility.Log("HighlightNextDecay: Initialised");
	}

    public void Start() => SceneManager.sceneLoaded += Load;
    public void OnDestroy() => SceneManager.sceneLoaded -= Load;

    public void Load(Scene scene, LoadSceneMode mode) {
        if (!(scene.name == "S3Menu"))
            return;
        try
        {
            cardHighlight = new ValueTracker<bool>("HighlightNextCard", new bool[2] {false, true}, UpdateHighlights);
            verbHighlight = new ValueTracker<bool>("HighlightNextVerb", new bool[2] {false, true}, UpdateHighlights);
            NoonUtility.Log("HighlightNextDecay: Trackers Started");
        }
        catch (Exception ex)
        {
          NoonUtility.LogException(ex);
        }
    }

    public void Update()
    {
        // placeholder, tracker unused
        UpdateHighlights(cardHighlight);
    }

    public static IEnumerable<Token> GetCards() {
        return Watchman.Get<HornedAxe>().GetExteriorSpheres()
            .Where<Sphere>(x => (double) x.TokenHeartbeatIntervalMultiplier > 0.0)
            .SelectMany<Sphere, Token>(x => x.GetTokens())
            .Where<Token>(x => x.Payload is ElementStack && ((ElementStack)x.Payload).Decays)
            .OrderBy(x => ((ElementStack)x.Payload).LifetimeRemaining);
    }

    public static IEnumerable<Token> GetSituations() {
        return Watchman.Get<HornedAxe>().GetExteriorSpheres()
            .Where<Sphere>(x => (double) x.TokenHeartbeatIntervalMultiplier > 0.0)
            .SelectMany<Sphere, Token>(x => x.GetTokens())
            .Where<Token>(x => x.Payload is Situation)
            .OrderBy(x => ((Situation)x.Payload).TimeRemaining);
    }

    public static void UpdateHighlights(SettingTracker<bool> _)
    {
        if (cardHighlight == null || verbHighlight == null)
            return;
        IEnumerable<Token> cards = GetCards();
        IEnumerable<Token> sits = GetSituations();
        IEnumerable<Token> sits_nonzero = sits.Where<Token>(x=>((Situation)x.Payload).TimeRemaining > 0.0f);
        if (cardHighlight.current && verbHighlight.current) {
            float cardtime = cards.Count() > 0 ? ((ElementStack)cards.ElementAt(0).Payload).LifetimeRemaining : 0.0f;
            float sittime = sits_nonzero.Count() > 0 ? ((Situation)sits_nonzero.ElementAt(0).Payload).TimeRemaining : 0.0f;
            if (cardtime > 0.0f && cardtime < sittime) {
                cardColor = primaryColor;
                verbColor = secondaryColor;
            } else if (sittime > 0.0f && sittime < cardtime) {
                cardColor = secondaryColor;
                verbColor = primaryColor;
            } else {
                cardColor = primaryColor;
                verbColor = primaryColor;
            }
        } else {
            cardColor = primaryColor;
            verbColor = primaryColor;
        }
        UpdateHighlights<ElementStack>(cardHighlight.current, cards);
        UpdateHighlights<Situation>(verbHighlight.current, sits);
    }

    public static void UpdateHighlights<T>(bool enable, IEnumerable<Token> tokens)
    {
        int index = 0;
        float smallest_lifetime = 0.0f;
        foreach (Token token in tokens)
        {
            float lifetime = typeof(T) == typeof(ElementStack)
                ? ((ElementStack)token.Payload).LifetimeRemaining
                : ((Situation)   token.Payload).TimeRemaining;
            Color setColor = typeof(T) == typeof(ElementStack) ? cardColor : verbColor;
            Traverse manifest = Traverse.Create(token).Field("_manifestation");
            Color current = manifest.Field("glowImage").Field("currentColor").GetValue<Color>();
            GraphicFader glow = manifest.Field("glowImage").GetValue<GraphicFader>();
            bool is_glowing = glow == null ? false : glow.gameObject.activeSelf;
            // highlight first (smallest value)
            if (enable && lifetime > 0.0f && (index == 0 || lifetime == smallest_lifetime || smallest_lifetime == 0.0f))
            {
                if (smallest_lifetime == 0.0f)
                {
                    smallest_lifetime = lifetime;
                }
                if (!is_glowing || (current != UIStyle.GetGlowColor(UIStyle.GlowPurpose.OnHover)
                    && current != UIStyle.GetGlowColor(UIStyle.GlowPurpose.Default)))
                {
                    object[] args;
                    if (current != setColor)
                    {
                        args = new object[1]{setColor};
                        manifest.Method("SetGlowColor", args).GetValue(args);
                        //refresh glow status
                        args = new object[2]{false, false};
                        manifest.Method("ShowGlow", args).GetValue(args);
                    }
                    args = new object[2]{true, false};
                    manifest.Method("ShowGlow", args).GetValue(args);
                }
            }
            // unhighlight others
            else if (is_glowing && (current == primaryColor || current == secondaryColor))
            {
                object[] args = new object[2]{false, false};
                manifest.Method("ShowGlow", args).GetValue(args);
            }
            token.UpdateVisuals();
            index++;
        }
    }

}

