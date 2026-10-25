shader_type sky;

// ---- gradient (driven from Sky.Tick each frame) ----
uniform vec3 sky_top : source_color = vec3(0.24, 0.42, 0.72);
uniform vec3 sky_horizon : source_color = vec3(0.78, 0.80, 0.82);
uniform vec3 ground_horizon : source_color = vec3(0.70, 0.66, 0.58);
uniform vec3 ground_bottom : source_color = vec3(0.34, 0.34, 0.32);
uniform float sky_energy = 1.0;

// ---- sun ----
uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);      // unit, TOWARD the sun
uniform vec3 sun_tint : source_color = vec3(1.0, 0.96, 0.86);
uniform float sun_disc = 1.0;                    // 0 hides it (night / heavy overcast)
uniform float sun_size = 0.014;                  // angular radius, radians

// ---- clouds ----
uniform float cloud_height = 900.0;
uniform float cloud_curve = 8.0;                 // fake planet radius, in deck heights (see deck_t)
uniform float cloud_density : hint_range(0.0, 1.0) = 1.0;
uniform vec3 cloud_lit : source_color = vec3(1.0, 0.99, 0.96);
uniform vec3 cloud_dark : source_color = vec3(0.42, 0.45, 0.52);
uniform vec2 cirrus_pan = vec2(0.0);
uniform float cirrus_amount : hint_range(0.0, 1.0) = 0.3;

// ---- moon ----
uniform vec3 moon_dir = vec3(0.0, -1.0, 0.0);    // unit, TOWARD the moon
uniform sampler2D moon_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform vec3 moon_tint : source_color = vec3(0.94, 0.95, 1.0);
uniform float moon_disc = 0.0;                   // 0 hides it (daylight / below the horizon)
uniform float moon_bright = 1.75;
uniform float moon_size = 0.075;                 // angular radius of the PADDED texture, radians
uniform float moon_halo = 0.30;

// ---- night ----
uniform float star_amount = 0.0;

uniform float bolt = 0.0;
uniform float bolt_azimuth = 0.0;
uniform float bolt_seed = 0.0;
const float BOLT_TOP = 0.30;
const float BOLT_WANDER = 0.05;
const float BOLT_CORE = 0.0022;
const float BOLT_GLOW = 0.018;
const float BRANCH_FROM = 0.35;
const float BRANCH_TO = 0.75;
const float CLOUD_FLASH_SPREAD = 2.2;
const float CLOUD_FLASH = 0.3;
const vec3 BOLT_COLOR = vec3(0.86, 0.90, 1.0);
#include "res://shaders/cloud_field.gdshaderinc"

float hash13(vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
}

// Distance along a view ray to a cloud shell at height h, on a fake planet of radius
// cloud_curve * h. A FLAT deck is the obvious choice and it's wrong: its distance runs away to
// infinity at the horizon, so the last few degrees of sky compress hundreds of tile repeats into a
// corduroy band. A curved shell bottoms out at sqrt(2*cloud_curve)*h instead — a couple of repeats,
// and those get swallowed by the aerial-perspective fade.
float deck_t(float up, float h) {
    float r = cloud_curve * h;
    return -r * up + sqrt(r * r * up * up + 2.0 * r * h + h * h);
}

// Cumulus deck + cirrus veil in the view direction, as (rgb, alpha) to composite over the sky.
vec4 clouds(vec3 dir, vec3 campos) {
    float up = max(dir.y, 0.0);
    float t = deck_t(up, cloud_height);
    float dist = t / deck_t(0.0, cloud_height);   // 0 straight overhead .. 1 at the horizon
    vec2 w = campos.xz + dir.xz * t;
    float horizon = smoothstep(-0.03, 0.05, dir.y);   // the deck stops at eye level

    float cov = cloud_cover_at(w, 0.0);

    // Fake self-shadowing, and the only thing giving these clouds any volume. The deck has no
    // thickness, so stand in for it: step TOWARD the sun along the deck and read the coverage there.
    // Cloud in that direction is cloud between this point and the sun, so the near-side face of a
    // puff (clear toward the sun) lights up and its far side falls into shadow. One tap.
    vec2 sun_step = normalize(vec2(sun_dir.x, sun_dir.z) + vec2(1e-5)) * (cloud_height * 0.45);
    float occ = cloud_cover_base(w + sun_step, 1.5);
    float lit = clamp(1.0 - occ * 0.85, 0.0, 1.0);
    vec3 cc = mix(cloud_dark, cloud_lit, lit);

    // Silver lining: strong forward scattering through the thin edge of a cloud near the sun.
    float fwd = pow(max(dot(dir, sun_dir), 0.0), 10.0);
    cc += sun_tint * fwd * (1.0 - cov) * 0.7 * sun_disc;
    cc += moon_tint * pow(max(dot(dir, moon_dir), 0.0), 14.0) * (1.0 - cov) * 0.5 * moon_disc;

    float ca = clamp(cov * cloud_density, 0.0, 1.0) * horizon;

    // Cirrus: a thinner, higher, faster layer with its own pan, so it visibly shears against the
    // deck below it. High threshold — cirrus is sparse ribbons, and anything less turns into a
    // milky sheet over the whole sky.
    float ci_a = 0.0;
    if (cirrus_amount > 0.001) {
        float ch = cloud_height * 1.6;
        float ct = deck_t(up, ch);
        vec2 cw = campos.xz + dir.xz * ct;
        float ci = texture(cloud_tex, (cw - cirrus_pan) * (cloud_scale * 0.4)).b;
        ci_a = smoothstep(0.58, 0.95, ci) * cirrus_amount * horizon
             * (1.0 - smoothstep(0.45, 0.95, ct / deck_t(0.0, ch)));
    }
    vec3 ci_col = mix(cloud_dark, cloud_lit, 0.85);

    float a = ca + ci_a * (1.0 - ca);
    vec3 rgb = a > 0.0001 ? (cc * ca + ci_col * ci_a * (1.0 - ca)) / a : vec3(0.0);

    // Aerial perspective: kilometres of atmosphere between here and the horizon deck wash it out to
    // the horizon colour and thin it. Also the last line of defence against the tile repeat.
    rgb = mix(rgb, sky_horizon * sky_energy, smoothstep(0.30, 0.95, dist) * 0.95);
    a *= 1.0 - smoothstep(0.60, 1.0, dist) * 0.7;
    return vec4(rgb, a);
}

float bolt_hash(float x, float seed) {
    return fract(sin(x * 12.9898 + seed * 78.233) * 43758.5453);
}

float bolt_path(float down, float seed) {
    float offset = 0.0;
    float amp = BOLT_WANDER;
    float freq = 5.0;
    for (int i = 0; i < 4; i++) {
        float segment = floor(down * freq);
        float a = bolt_hash(segment, seed + float(i) * 3.1) - 0.5;
        float b = bolt_hash(segment + 1.0, seed + float(i) * 3.1) - 0.5;
        offset += mix(a, b, fract(down * freq)) * amp;
        amp *= 0.5;
        freq *= 2.3;
    }
    return offset;
}

float bolt_line(float away, float width) {
    return smoothstep(width, 0.0, abs(away)) * 3.0 + exp(-abs(away) / BOLT_GLOW) * 0.35;
}

vec3 lightning(vec3 dir, float azimuth_gap) {
    if (dir.y < -0.01 || dir.y > BOLT_TOP) return vec3(0.0);
    float down = 1.0 - dir.y / BOLT_TOP;
    float trunk = azimuth_gap - bolt_path(down, bolt_seed);
    float light = bolt_line(trunk, BOLT_CORE);
    if (down > BRANCH_FROM && down < BRANCH_TO) {
        float along = (down - BRANCH_FROM) / (BRANCH_TO - BRANCH_FROM);
        float fork = trunk - along * (bolt_hash(1.0, bolt_seed) - 0.5) * 0.12 - bolt_path(down * 1.7, bolt_seed + 9.0) * 0.6;
        light += bolt_line(fork, BOLT_CORE * 0.6) * (1.0 - along) * 0.6;
    }
    return BOLT_COLOR * light * bolt;
}

void sky() {
    vec3 dir = EYEDIR;
    float up = dir.y;

    // Gradient. pow(1-|up|, 4) keeps the horizon band tight and the zenith broad, which is what
    // makes a two-colour ramp read as depth rather than as a wash.
    vec3 col = up >= 0.0
        ? mix(sky_top, sky_horizon, pow(1.0 - up, 4.0))
        : mix(ground_bottom, ground_horizon, pow(1.0 + up, 4.0));
    col *= sky_energy;

    // Sun disc, drawn BEFORE the clouds so they occlude it. Kept modest in HDR: the glow pass is
    // capped at 1.25 luminance, and a hotter disc just smears a halo over half the sky.
    if (sun_disc > 0.001) {
        float sc = dot(dir, sun_dir);
        float ang = acos(clamp(sc, -1.0, 1.0));
        float disc = 1.0 - smoothstep(sun_size * 0.8, sun_size * 1.4, ang);
        float glow = pow(max(sc, 0.0), 250.0) * 0.30 + pow(max(sc, 0.0), 6.0) * 0.05;
        col += sun_tint * (disc * 2.0 + glow) * sun_disc;
    }

    // Moon, before the clouds so the deck occludes it per pixel -- ATMOSPHERE_REFERENCE.md "The moon".
    if (moon_disc > 0.001) {
        vec3 mx = normalize(cross(moon_dir, vec3(0.0, 1.0, 0.0)));
        vec3 my = cross(mx, moon_dir);
        vec2 p = vec2(dot(dir, mx), dot(dir, my)) / moon_size;
        float md = dot(dir, moon_dir);
        if (md > 0.0 && abs(p.x) < 1.0 && abs(p.y) < 1.0) {
            vec4 face = texture(moon_tex, vec2(p.x, -p.y) * 0.5 + 0.5);
            float a = face.a * moon_disc;
            col = mix(col, moon_tint * face.rgb * moon_bright, a);
        }
        float ang = acos(clamp(md, -1.0, 1.0));
        float halo = exp(-ang / (moon_size * 0.55)) + exp(-ang / (moon_size * 3.5)) * 0.18;
        col += moon_tint * moon_halo * moon_disc * halo;
    }

    // Stars: a hashed 3D lattice along the view ray. star_amount is 0 by day, so the branch is
    // uniform across the frame and costs nothing until dusk.
    vec3 stars = vec3(0.0);
    if (star_amount > 0.001 && up > -0.03) {
        vec3 sp = dir * 260.0;
        vec3 cell = floor(sp);
        float r = hash13(cell);
        float bright = smoothstep(0.972, 1.0, r);
        if (bright > 0.0) {
            vec3 off = vec3(hash13(cell + 11.3), hash13(cell + 27.7), hash13(cell + 43.1)) - 0.5;
            float d = length(fract(sp) - 0.5 - off * 0.55);
            stars = vec3(0.86, 0.90, 1.0) * smoothstep(0.20, 0.0, d) * bright
                  * star_amount * (0.35 + 0.65 * hash13(cell + 5.0))
                  * smoothstep(-0.02, 0.18, up);          // haze swallows them near the horizon
        }
    }

    vec4 cl = clouds(dir, POSITION);
    float azimuth_gap = 0.0;
    if (bolt > 0.001) {
        azimuth_gap = mod(atan(dir.x, dir.z) - bolt_azimuth + PI, TAU) - PI;
        cl.rgb += BOLT_COLOR * bolt * exp(-abs(azimuth_gap) * CLOUD_FLASH_SPREAD) * CLOUD_FLASH;
    }
    col = mix(col, cl.rgb, cl.a);
    col += stars * (1.0 - cl.a);                          // stars only shine through the gaps
    if (bolt > 0.001 && !AT_CUBEMAP_PASS) col += lightning(dir, azimuth_gap);

    // Ordered-ish dither. A two-colour sky gradient across 1080p lands squarely in 8-bit banding
    // territory; one hashed LSB of noise removes it for free. Meaningless in the cubemap pass.
    if (!AT_CUBEMAP_PASS) {
        float dth = fract(sin(dot(SCREEN_UV, vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
        col += dth * (1.5 / 255.0);
    }

    COLOR = col;
}
