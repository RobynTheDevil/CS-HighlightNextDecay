using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
    public static ValueTracker<bool> showHighlight {get; private set;}
    public static Color color = UIStyle.brightPink;

    public void Update()
    {
        HighlightNextDecay.UpdateHighlights(showHighlight.current);
    }

    public void Start() {
        try
        {
            HighlightNextDecay.showHighlight = new ValueTracker<bool>("HighlightNextDecay", new bool[2] {false, true}, WhenSettingUpdated);
            NoonUtility.Log("HighlightNextDecay: Trackers Started");
        }
        catch (Exception ex)
        {
          NoonUtility.LogException(ex);
        }
    }

    public static void Initialise() {
        //Harmony.DEBUG = true;
        //Patch.harmony = new Harmony("robynthedevil.highlightnextdecay");
		new GameObject().AddComponent<HighlightNextDecay>();
        NoonUtility.Log("HighlightNextDecay: Initialised");
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

    public static void WhenSettingUpdated(SettingTracker<bool> tracker) {
        NoonUtility.Log(string.Format("HighlightNextDecay: Setting Updated {0}", tracker.current));
        HighlightNextDecay.UpdateHighlights(tracker.current);
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
                if (!is_glowing)
                {
                    object[] args = new object[1]{HighlightNextDecay.color};
                    manifest.Method("SetGlowColor", args).GetValue(args);
                }
                current = manifest.Field("glowImage").Field("currentColor").GetValue<Color>();
                if (!is_glowing && current == HighlightNextDecay.color)
                {
                    object[] args = new object[2]{true, false};
                    manifest.Method("ShowGlow", args).GetValue(args);
                }
                token.UpdateVisuals();
            }
            // unhighlight others
            else if (is_glowing && current == HighlightNextDecay.color)
            {
                object[] args = new object[2]{false, false};
                manifest.Method("ShowGlow", args).GetValue(args);
            }
            NoonUtility.Log(string.Format("HighlightNextDecay: Update ind {0}, {1}, {2}, {3}, {4}", index, lifetime, current, glow, is_glowing));
            index++;
        }
    }

    public static void UpdateHighlights(bool enable) {
        IEnumerable<Token> cards = HighlightNextDecay.GetCards();
        HighlightNextDecay.UpdateHighlights<ElementStack>(enable, cards);
        IEnumerable<Token> sits = HighlightNextDecay.GetSituations();
        HighlightNextDecay.UpdateHighlights<Situation>(enable, sits);
    }

}

