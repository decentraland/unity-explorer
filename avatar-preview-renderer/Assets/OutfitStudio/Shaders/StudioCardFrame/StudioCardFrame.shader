Shader "Custom/StudioCardFrame"
{
    // Editor-only Outfit Studio "item card" frame, drawn as four camera-parented quads:
    //   _Mode 0 = Card panel  (rounded-rect, painted with the Decentraland vignette+pattern, ZWrite On)
    //   _Mode 1 = Bottom fade (the SAME paint at the same UV → seamless, × a vertical fade to clear)
    //   _Mode 2 = Side mask   (ERASES whatever is outside the card rect; the top stays open)
    // Modes 1 and 2 both clip the fragments they don't cover and run with ZWrite On, so each resets depth
    // over exactly the pixels it repaints/erases — that's what keeps the depth-tested border honest.
    //   _Mode 3 = Border      (rounded-rect ring, last in the queue so it beats fade/mask, but
    //                          depth-tested so the avatar occludes it — 2026-08-06)
    // There is deliberately NO background layer (2026-07-30): whatever the camera clears to shows
    // outside the card, so a still exports with only the card + avatar opaque and everything else
    // transparent. Render state (ZTest/ZWrite/Blend) and the queue are driven from the material by
    // StudioCardFrame.cs so one shader covers all four layers. See IMPLEMENTATION.md §18.
    Properties
    {
        [Enum(Card,0,Fade,1,SideMask,2,Border,3)] _Mode ("Mode", Float) = 0

        // Side-mask rect (mode 2), in the fullscreen mask quad's UV space: (left, right, bottom, top).
        _MaskRect ("Mask Rect (l,r,b,t)", Vector) = (0, 1, 0, 1)
        // SideMask quad only: 1 leaves the column above the card top uncropped so an avatar's head can
        // overflow (the default card look); 0 crops the top like every other edge, for item cards where
        // the subject belongs fully inside the rounded rect.
        _MaskTopOpen ("Mask Top Open", Range(0,1)) = 1

        // The card's paint — the animated purple vignette + scrolling icon pattern from Explorer's
        // loading screens, ported from Custom/AnimatedBackgroundMovingTexture (unity-explorer's
        // TileableTexture.shader / BackgroundLoading.mat). Inner/Outer are the two artist-facing
        // colours; everything else is a fixed constant from that material. See IMPLEMENTATION.md §18.
        _DclOverlayTex ("DCL Overlay Tex", 2D) = "white" {}
        _DclInnerColor ("DCL Inner Color", Color) = (0.75, 0, 1, 1)
        _DclOuterColor ("DCL Outer Color", Color) = (0.3, 0, 0.5, 1)
        _DclRadius ("DCL Radius", Range(0,1)) = 0.167
        _DclSmoothness ("DCL Smoothness", Range(0.01,1)) = 0.5
        _DclOverlayColor ("DCL Overlay Color", Color) = (1, 1, 1, 1)
        _DclOverlayTiling ("DCL Overlay Tiling", Float) = 1.66
        // The card's height as a fraction of the frame's, driven from StudioCardFrame.PushParams() —
        // keeps the pattern's on-screen icon size (and scroll speed) matching the reference whatever
        // the card margins are. See the tiling comment in DclCardPaint().
        _DclTileScale ("DCL Tile Scale (internal)", Float) = 1.0
        _DclOverlayDirection ("DCL Overlay Direction", Vector) = (1, -1.25, 0, 0)
        _DclOverlaySpeed ("DCL Overlay Speed", Float) = 0.06
        _DclOverlayAlpha ("DCL Overlay Alpha", Range(0,1)) = 0.573
        // 0 = pattern-less card (just the inner/outer vignette); driven from
        // StudioCardFrame.PatternEnabled, not exposed as a texture-absence hack any more.
        _DclPatternEnabled ("DCL Pattern Enabled", Range(0,1)) = 1
        _DclGlowColor ("DCL Glow Color", Color) = (0.66, 0, 0.745, 1)
        _DclGlowStrength ("DCL Glow Strength", Float) = 0.59
        // Reference material has this off-center at (0.68, 0.5); kept centered here on purpose so the
        // hotspot sits behind the avatar rather than beside it (Mauricio, 2026-07-27 and again on
        // 2026-07-30 after the parity pass) — the one deliberate deviation from BackgroundLoading.mat.
        _DclGlowCenter ("DCL Glow Center", Vector) = (0.5, 0.5, 0, 0)
        _DclGlowRadius ("DCL Glow Radius", Vector) = (0.05, -0.13, 0, 0)
        _DclGlowSmoothness ("DCL Glow Smoothness", Float) = 3.61
        _DclLuminosityStrength ("DCL Luminosity Strength", Range(0,1)) = 0.541

        // Card rounded-rect (also used by the fade so its bottom corners match)
        _CardAspect ("Card Aspect (w/h)", Float) = 0.66
        _CornerRadius ("Corner Radius", Range(0,1)) = 0.08
        _BorderColor ("Border Color", Color) = (0.72, 0.55, 0.88, 1)
        _InnerBorderWidth ("Inner Border Width", Range(0,0.2)) = 0.0
        _OuterBorderWidth ("Outer Border Width", Range(0,0.2)) = 0.0
        _BorderTopFade ("Border Top Fade Start (uv.y)", Range(0,1)) = 0.88
        // Border quad only: how much bigger (as a scale factor) the border's own quad is than the
        // card's, so the shader has room to paint the outer ring beyond the card edge. Driven from
        // StudioCardFrame.Layout(), not user-facing. See the mode-3 UV remap below.
        _BorderOversize ("Border Oversize (internal)", Float) = 1.0

        // Fade (mode 1)
        _FadeStart ("Fade Start (uv.y)", Range(0,1)) = 0.18
        _FadeEnd ("Fade End (uv.y)", Range(0,1)) = 0.4

        // Material-driven render state
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4   // LEqual
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1        // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0        // Zero
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src Blend Alpha", Float) = 1 // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst Blend Alpha", Float) = 10 // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StudioCardFrame"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            // Separate alpha blend, both pairs material-driven. RGB uses the per-layer factors; alpha
            // defaults to the standard "over" formula (One, OneMinusSrcAlpha) for every layer EXCEPT
            // the side mask, which needs (Zero, OneMinusSrcAlpha) in both pairs so it can ERASE to
            // fully transparent rather than paint (see mode 2).
            //
            // Why alpha needs its own pair at all: with a single shared factor pair, alpha blends as
            // srcAlpha² + dstAlpha·(1-srcAlpha), which dips below 1 (as low as 0.75) at any
            // anti-aliased edge composited over an opaque layer beneath — invisible in RGB (the
            // painted color matches what's underneath) but a visible seam in the alpha channel alone
            // (e.g. compositing the exported PNG over a different background). The "over" formula
            // keeps alpha at 1 wherever the destination is already opaque, so the card and the Fade
            // quad's bottom gradient export fully opaque, as intended.
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float _Mode;
            float _CardAspect, _CornerRadius, _InnerBorderWidth, _OuterBorderWidth, _BorderTopFade, _BorderOversize;
            float4 _BorderColor;
            float _FadeStart, _FadeEnd;
            float4 _MaskRect;
            float _MaskTopOpen;

            TEXTURE2D(_DclOverlayTex);
            SAMPLER(sampler_DclOverlayTex);
            float4 _DclInnerColor, _DclOuterColor;
            float _DclRadius, _DclSmoothness;
            float4 _DclOverlayColor;
            float _DclOverlayTiling, _DclTileScale;
            float2 _DclOverlayDirection;
            float _DclOverlaySpeed, _DclOverlayAlpha, _DclPatternEnabled;
            float4 _DclGlowColor;
            float _DclGlowStrength;
            float2 _DclGlowCenter, _DclGlowRadius;
            float _DclGlowSmoothness, _DclLuminosityStrength;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // RGB <-> HSV, used by the card paint's "luminosity blend" (recolors the overlay pattern to
            // the vignette's hue/saturation while keeping the pattern's own brightness).
            float3 RgbToHsv (float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb (float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Ported from Explorer's Custom/AnimatedBackgroundMovingTexture (TileableTexture.shader):
            // a radial purple vignette with a scrolling, tinted icon-pattern overlay (luminosity blend)
            // and a radial glow. Returns colour only — the caller owns alpha (the rounded-rect mask).
            // `uv` is the CARD's 0..1 space (both the card and fade quads share that transform), so the
            // vignette is centred on the card and its corners reach the outer colour at UV radius 0.707.
            float3 DclCardPaint (float2 uv)
            {
                float radius = length(uv - 0.5);
                float mask = smoothstep(_DclRadius + _DclSmoothness, _DclRadius, radius);
                float3 vignette = lerp(_DclOuterColor.rgb, _DclInnerColor.rgb, mask);

                // Explorer tiles a fullscreen quad by the SCREEN aspect (keeping icons square there).
                // Here the UV spans the card, so tile by the card's own aspect instead — and scale both
                // axes by _DclTileScale (the card's height as a fraction of the frame's) so one tile
                // still covers the same number of on-screen pixels as it does in the reference. That
                // keeps icon size identical regardless of the card margins, and because the scroll
                // offset below is in tile units, it keeps the scroll speed in px/s identical too.
                float2 tiling = _DclOverlayTiling * _DclTileScale * float2(_CardAspect, 1.0);
                float2 overlayUv = uv * tiling;
                overlayUv += _Time.y * _DclOverlayDirection * _DclOverlaySpeed;
                float4 overlay = SAMPLE_TEXTURE2D(_DclOverlayTex, sampler_DclOverlayTex, overlayUv) * _DclOverlayColor;
                overlay.a *= _DclOverlayAlpha * mask * _DclPatternEnabled;

                float3 vignetteHsv = RgbToHsv(vignette);
                float3 overlayHsv = RgbToHsv(overlay.rgb);
                float v = lerp(0.5, 1.0, overlayHsv.z);
                float3 luminosityBlend = HsvToRgb(float3(vignetteHsv.x, vignetteHsv.y, v));
                float3 col = lerp(vignette, luminosityBlend, overlay.a * _DclLuminosityStrength);

                float2 glowDelta = (uv - _DclGlowCenter) / _DclGlowRadius;
                float glowMask = 1.0 - smoothstep(1.0, 1.0 + _DclGlowSmoothness, length(glowDelta));
                // The reference builds the glow as a full float4 (`_GlowColor * glowMask * _GlowStrength`)
                // and then adds `glow.rgb * glow.a` — so mask AND strength each land on the result
                // TWICE. Replicated verbatim rather than folded into one multiply: the material's tuned
                // _GlowStrength 0.59 is really an effective 0.35, and the squared mask makes the hotspot
                // much tighter. Linearising it (as the first port did) blew the glow out into a wide
                // bright wash that flattened the vignette.
                float glow = glowMask * _DclGlowStrength;
                col += _DclGlowColor.rgb * glow * glow * _DclGlowColor.a;
                return col;
            }

            // Signed distance to a rounded box; negative inside. Worked in a space where the card
            // half-height is 1 and half-width is the aspect, so the corner radius stays circular.
            float RoundedBoxSDF (float2 uv, float aspect, float radius)
            {
                float2 e = float2(aspect, 1.0);
                float r = min(radius, min(e.x, e.y));
                float2 p = (uv - 0.5) * 2.0 * e;
                float2 q = abs(p) - (e - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // --- Side mask (mode 2) — works in the fullscreen quad's own UV, not the card's -----
                if (_Mode > 1.5 && _Mode < 2.5)
                {
                    // ERASE everything OUTSIDE _MaskRect (the avatar's arms/hands spilling past the card
                    // sides, its feet hanging below the bottom), but leave the top open above the rect so
                    // the head still overflows. Erasing, not repainting: there's no background layer to
                    // repaint with any more, and the material's Zero/OneMinusSrcAlpha blend (both pairs)
                    // turns the alpha below into dst *= 1-srcAlpha, i.e. colour AND alpha go to 0
                    // wherever this writes alpha 1.
                    //
                    // _MaskRect is NOT necessarily the card rect: PushParams() pushes the edges the user
                    // isn't masking far out of frame (and restates _CardAspect/_CornerRadius to match),
                    // so "sides only" and "bottom only" both fall out of this same code — an edge that's
                    // switched off simply has no geometry near the frame to cut against.
                    float2 lo = _MaskRect.xz, hi = _MaskRect.yw;                 // (left,bottom),(right,top)
                    float2 cardUv = (uv - lo) / max(hi - lo, 1e-4);
                    float md = RoundedBoxSDF(cardUv, _CardAspect, _CornerRadius);
                    float maa = max(fwidth(md), 1e-5);
                    float cardMask = 1.0 - smoothstep(-maa, maa, md);           // inside the rounded card
                    float axu = max(fwidth(uv.x), 1e-5), ayu = max(fwidth(uv.y), 1e-5);
                    float withinX = smoothstep(lo.x - axu, lo.x + axu, uv.x)
                                  * (1.0 - smoothstep(hi.x - axu, hi.x + axu, uv.x));
                    // Open above the card top, unless _MaskTopOpen closes it (item cards) — zeroing the
                    // term leaves `inside` as the card mask alone, so the top crops like any other edge.
                    float aboveTop = smoothstep(hi.y - ayu, hi.y + ayu, uv.y) * _MaskTopOpen;
                    // ADD (not max) the two keep-regions: at the card-top transition both the card
                    // mask and the overflow column are mid-fade (~0.5), and max(0.5,0.5)=0.5 dipped
                    // "inside" below 1, eroding a faint line across the head. Their sum is ~1 there
                    // (they're complementary in y), so the seam disappears; saturate caps it and they
                    // never both fully overlap elsewhere (aboveTop is 0 below the top).
                    float inside = saturate(cardMask + withinX * aboveTop);

                    // Discard the keep-region so ONLY the erasing fragments survive — visually identical
                    // (they wrote alpha 0, i.e. a no-op blend) but it makes this layer's ZWrite On
                    // meaningful: the erased region resets depth to the card plane, while the kept region
                    // leaves the avatar's depth intact.
                    //
                    // Why that matters (2026-08-06): erasing colour does NOT erase depth, so avatar
                    // geometry this mask has just wiped out was still occluding the depth-tested border
                    // drawn afterwards — "mask avatar below card" punched gaps in the card's own ring
                    // wherever a leg crossed it. Resetting depth here is what keeps "the avatar occludes
                    // the border" true for the avatar you can SEE rather than the avatar that was drawn.
                    clip((1.0 - inside) - 1e-3);
                    return float4(0.0, 0.0, 0.0, 1.0 - inside);                  // erase only outside
                }

                // Border's quad is oversized relative to the card (see _BorderOversize / the C#
                // Layout() comment) so there's physical room to paint a ring outside the card edge.
                // Remap its raw UV back into the same normalized card space Card/Fade use — where
                // the card edge sits at dist==0 — before the shared SDF below runs.
                if (_Mode > 2.5) uv = (uv - 0.5) * _BorderOversize + 0.5;

                // Shared rounded-rect mask for card + fade (+ border, after the remap above)
                float dist = RoundedBoxSDF(uv, _CardAspect, _CornerRadius);
                float aa = max(fwidth(dist), 1e-5);
                float mask = 1.0 - smoothstep(-aa, aa, dist);                    // 1 inside, 0 outside

                // --- Card panel (mode 0) — fill only; the border is a separate top layer -----------
                if (_Mode < 0.5)
                {
                    // The card is the only depth-writing layer now that the background quad is gone
                    // (it stands in for it: without SOME ZWrite-On layer the skybox, drawn after the
                    // opaque queue, would paint straight over the card in any view whose clear is set
                    // to Skybox — e.g. the Scene view). Discard the fully-transparent pixels so only
                    // the rounded card itself writes depth, leaving the four corner notches clear.
                    clip(mask - 1e-3);
                    return float4(DclCardPaint(uv), mask);
                }

                // --- Bottom fade (mode 1) -------------------------------------------------------
                // Same paint at the same UV as the card (the two quads share a transform), so the fade
                // is a pure alpha ramp over an identical colour — no seam, and nothing to keep in sync.
                if (_Mode < 1.5)
                {
                    float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, uv.y);   // opaque at bottom
                    float a = mask * fade;

                    // Same depth-honesty fix as the side mask above, for the same reason: the fade paints
                    // the card's own colour over the legs without clearing the depth they wrote, so the
                    // depth-tested border's bottom stroke came out with gaps wherever a leg crossed it.
                    // Clipping the fully-transparent pixels (a no-op blend anyway) leaves this layer's
                    // ZWrite On resetting depth to the card plane exactly across the region it has
                    // repainted. Inside the gradient the leg is still partly visible and the border now
                    // draws in front of it — deliberate: a continuous ring reads better than a stroke that
                    // dips behind a half-faded shin, and it matches the pre-2026-08-06 look at the bottom.
                    clip(a - 1e-3);
                    return float4(DclCardPaint(uv), a);
                }

                // --- Border (mode 3) — drawn LAST, on top of the avatar / fade / side-mask ------
                // Two rings straddling the card edge (dist == 0): an inner one in the band
                // dist ∈ (-_InnerBorderWidth, 0) and an outer one in dist ∈ (0, _OuterBorderWidth),
                // each written as the difference of two edge smoothsteps so it collapses to EXACTLY
                // zero when its width is 0 — the old mask*innerCut form peaked at ~0.25 on the edge,
                // leaving a ~1px hairline around the whole card even at width 0.
                //
                // Each ring's edge-side cutoff is nudged by ~aa (one pixel) past dist 0, into the
                // OTHER ring's territory (inner reaches ~1px outward, outer reaches ~1px inward),
                // via a per-ring `bias` fed into the SAME cutoff formula dist compares against — not
                // a separately-centered smoothstep. That keeps the cutoff identical in form to the
                // ring's own smoothstep whenever its width is 0 (bias also collapses to 0 then), so
                // the "collapses to EXACTLY zero" invariant above still holds. Without this bias,
                // whichever width was 0 left the OTHER ring's cutoff centred exactly on dist 0 —
                // fine when both rings compensate each other's 50%, but the moment only one width
                // was non-zero its own cutoff alone still only reached 50% coverage right on the
                // seam, showing whatever's underneath (avatar/background) through as a ~1px line.
                float innerBias = aa * smoothstep(0.0, aa, _InnerBorderWidth);
                float outerBias = aa * smoothstep(0.0, aa, _OuterBorderWidth);
                float sInner = smoothstep(-_InnerBorderWidth - aa, -_InnerBorderWidth + aa, dist);     // 0 deep-inside → 1 inward of ring
                float sInnerCutoff = smoothstep(-aa, aa, dist - innerBias);                            // ring stays full through dist 0 (and ~innerBias past it)
                float sOuter = smoothstep(_OuterBorderWidth - aa, _OuterBorderWidth + aa, dist);        // 0 within outer band → 1 beyond it
                float sOuterCutoff = smoothstep(-aa, aa, dist + outerBias);                             // ring stays full through dist 0 (and ~outerBias before it)
                float ring = saturate(saturate(sInner - sInnerCutoff) + saturate(sOuterCutoff - sOuter));
                float topOpen = 1.0 - smoothstep(_BorderTopFade, 1.0, uv.y);
                return float4(_BorderColor.rgb, ring * topOpen * _BorderColor.a);
            }
            ENDHLSL
        }
    }
}
