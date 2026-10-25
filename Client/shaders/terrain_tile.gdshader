shader_type spatial;
render_mode cull_disabled;
uniform sampler2DArray tiles : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform float cell_size = 4.0;
global uniform float weather_wetness;
global uniform float weather_rain;
global uniform float weather_snow;
global uniform vec3 weather_sky;
varying flat float v_l1;
varying flat float v_l2;
varying flat float v_has2;
varying vec2 v_grid;
varying vec3 v_world;
varying float v_up;

const float FLAT_FROM = 0.80;
const float FLAT_TO = 0.97;
const float DAMP_DARKEN = 0.78;
const float DAMP_ROUGHNESS = 0.6;
const float DAMP_SPECULAR = 0.6;
const float PUDDLE_SCALE = 0.085;
const float PUDDLE_FROM = 0.60;
const float PUDDLE_TO = 0.68;
const float PUDDLE_WET_FROM = 0.35;
const float PUDDLE_DARKEN = 0.55;
const vec3 PUDDLE_TINT = vec3(0.003, 0.004, 0.006);
const float PUDDLE_ROUGHNESS = 0.18;
const float PUDDLE_MIRROR = 0.38;
const float PUDDLE_MIRROR_FLOOR = 0.05;
const float SHEEN_SCALE = 0.35;
const float SHEEN_FLOOR = 0.45;
const float RIPPLE_DENSITY = 1.8;
const float RIPPLE_SPEED = 0.9;
const float RIPPLE_RADIUS = 0.42;
const float RIPPLE_WIDTH = 0.07;
const float RIPPLE_FREQ = 60.0;
const float RIPPLE_STRENGTH = 0.35;
const float SNOW_SCALE = 0.22;
const float SNOW_MAX = 0.82;
const float SNOW_SLOPE_SHARE = 0.12;
const vec3 SNOW_COLOR = vec3(0.86, 0.89, 0.93);

vec3 linear_to_srgb(vec3 c) {
    vec3 lo = c * 12.92;
    vec3 hi = 1.055 * pow(max(c, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055;
    return mix(lo, hi, step(vec3(0.0031308), c));
}

vec3 srgb_to_linear(vec3 c) {
    vec3 lo = c / 12.92;
    vec3 hi = pow(max((c + 0.055) / 1.055, vec3(0.0)), vec3(2.4));
    return mix(lo, hi, step(vec3(0.04045), c));
}

float hash12(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x),
               mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

float patches(vec2 p) {
    return value_noise(p) * 0.65 + value_noise(p * 2.7 + 17.0) * 0.35;
}

vec2 ripples(vec2 p, float t) {
    vec2 cell = floor(p);
    vec2 slope = vec2(0.0);
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 c = cell + vec2(float(x), float(y));
            vec2 centre = c + vec2(hash12(c), hash12(c + 7.31));
            float phase = fract(t * RIPPLE_SPEED + hash12(c + 3.17));
            vec2 to = p - centre;
            float d = length(to);
            float k = d - phase * RIPPLE_RADIUS;
            float ring = cos(k * RIPPLE_FREQ) * (1.0 - phase) * smoothstep(RIPPLE_WIDTH, 0.0, abs(k));
            slope += to / max(d, 1e-4) * ring;
        }
    }
    return slope;
}

void vertex() {
    v_l1 = CUSTOM0.r;
    v_l2 = CUSTOM0.g;
    v_has2 = CUSTOM0.b;
    v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_grid = v_world.xz / cell_size;
    v_up = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz).y;
}
// Cell UV is the grid fraction under a .gtd dir flip: tile's orientation, grid's scale (handoff §8).
vec2 tile_grad(vec2 duv, float len) {
    return duv * (len / max(length(duv), 1e-8));
}

void fragment() {
    float lx = length(dFdx(v_grid)), ly = length(dFdy(v_grid));
    vec3 albedo = textureGrad(tiles, vec3(UV, v_l1),
                              tile_grad(dFdx(UV), lx), tile_grad(dFdy(UV), ly)).rgb;
    if (v_has2 > 0.5) {
        vec3 c2 = textureGrad(tiles, vec3(UV2, v_l2),
                              tile_grad(dFdx(UV2), lx), tile_grad(dFdy(UV2), ly)).rgb;
        vec3 fixed_function_add = min(linear_to_srgb(albedo) + linear_to_srgb(c2), vec3(1.0));
        albedo = srgb_to_linear(fixed_function_add);
    }
    float roughness = 1.0;
    float specular = 0.5;
    float flat_ground = smoothstep(FLAT_FROM, FLAT_TO, v_up);

    float snow = 0.0;
    if (weather_snow > 0.001) {
        float drift = patches(v_world.xz * SNOW_SCALE);
        float lying = mix(SNOW_SLOPE_SHARE, 1.0, flat_ground);
        snow = clamp(smoothstep(1.0 - weather_snow, 1.06 - weather_snow * 0.75, drift) * lying, 0.0, SNOW_MAX) * weather_snow;
        albedo = mix(albedo, SNOW_COLOR, snow);
    }

    float wet = weather_wetness * (1.0 - snow);
    if (wet > 0.001) {
        float puddle = smoothstep(PUDDLE_FROM, PUDDLE_TO, patches(v_world.xz * PUDDLE_SCALE))
                     * flat_ground * smoothstep(PUDDLE_WET_FROM, 1.0, wet);
        albedo *= mix(1.0, DAMP_DARKEN, wet);
        albedo = mix(albedo, albedo * PUDDLE_DARKEN + PUDDLE_TINT, puddle);
        roughness = mix(mix(1.0, DAMP_ROUGHNESS, wet), PUDDLE_ROUGHNESS, puddle);
        specular = mix(specular, DAMP_SPECULAR, wet);
        if (puddle > 0.01 && weather_rain > 0.01) {
            vec2 slope = ripples(v_world.xz * RIPPLE_DENSITY, TIME) * RIPPLE_STRENGTH * weather_rain;
            vec3 rippled = normalize((VIEW_MATRIX * vec4(normalize(vec3(-slope.x, 1.0, -slope.y)), 0.0)).xyz);
            NORMAL = normalize(mix(NORMAL, rippled, puddle));
        }
        float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
        float mirror = mix(PUDDLE_MIRROR_FLOOR, 1.0, pow(1.0 - facing, 3.0));
        float sheen = mix(SHEEN_FLOOR, 1.0, value_noise(v_world.xz * SHEEN_SCALE));
        EMISSION = weather_sky * mirror * sheen * PUDDLE_MIRROR * puddle;
    }

    ALBEDO = albedo;
    ROUGHNESS = roughness;
    SPECULAR = specular;
}
