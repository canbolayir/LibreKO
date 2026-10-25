shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform float aspect = 1.7778;
uniform float bend = 0.05;

const float BEAD_ROWS = 7.0;
const float BEAD_SHARE = 0.16;
const float RUNNER_COLUMNS = 6.0;
const float RUNNER_SHARE = 0.32;
const float RUNNER_HEIGHT = 0.036;
const float RUNNER_WIDTH = 0.22;
const float STICK = 0.62;
const float SLIP_LENGTH = 0.16;
const float WANDER = 0.09;
const float TRAIL_LENGTH = 0.28;
const float TRAIL_WIDTH = 0.06;
const float TRAIL_WET = 0.5;
const float LEFTOVER_ROWS = 70.0;
const float LEFTOVER_SHARE = 0.28;
const float EDGE_SHADE = 0.4;
const float SHINE = 0.45;
const float WATER_LIGHT = 0.08;
const vec2 SHINE_AT = vec2(-0.3, -0.45);

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

vec3 beads(vec2 uv, float t) {
    vec2 grid = uv * vec2(BEAD_ROWS * aspect, BEAD_ROWS);
    vec2 cell = floor(grid);
    vec2 local = fract(grid) - 0.5;
    float seed = hash(cell);
    float shown = step(seed, BEAD_SHARE);
    vec2 centre = vec2(hash(cell + 3.1), hash(cell + 7.7)) * 0.5 - 0.25;
    float life = fract(t * (0.03 + seed * 0.08) + hash(cell + 1.3));
    float fade = smoothstep(0.0, 0.2, life) * smoothstep(1.0, 0.7, life);
    float radius = mix(0.10, 0.22, hash(cell + 5.2)) * fade + 0.0001;
    vec2 normal = (local - centre) / radius;
    float cover = smoothstep(1.0, 0.75, length(normal)) * shown;
    return vec3(normal, cover);
}

float wander(float y, float seed) {
    return (sin(y * 17.0 + seed * 40.0) * 0.6 + sin(y * 6.3 + seed * 13.0) * 0.4) * WANDER;
}

vec3 runners(vec2 uv, float t) {
    float x = uv.x * RUNNER_COLUMNS * aspect;
    float column = floor(x);
    float seed = hash(vec2(column, 11.0));
    float shown = step(seed, RUNNER_SHARE);
    float travel = t * mix(0.35, 0.8, hash(vec2(column, 4.0))) + seed * 9.0;
    float slip = floor(travel) + smoothstep(STICK, 1.0, fract(travel));
    float head = fract(slip * SLIP_LENGTH + seed * 3.7) * 1.3 - 0.15;
    float lane = fract(x) - 0.5 - (hash(vec2(column, 2.0)) - 0.5) * 0.4;
    float below = uv.y - head;

    float across = lane - wander(head, seed);
    vec2 shape = vec2(across / RUNNER_WIDTH, below / (RUNNER_HEIGHT * (below > 0.0 ? 0.8 : 1.3)));
    vec3 drop = vec3(shape, smoothstep(1.0, 0.75, length(shape)));

    float trail_across = lane - wander(uv.y, seed);
    float behind = smoothstep(-TRAIL_LENGTH, 0.0, below) * step(below, 0.0);
    float trail = smoothstep(TRAIL_WIDTH, 0.0, abs(trail_across)) * behind * TRAIL_WET;
    vec2 leftover_cell = vec2(column, floor(uv.y * LEFTOVER_ROWS));
    float leftover_y = fract(uv.y * LEFTOVER_ROWS) - 0.5;
    vec2 bead = vec2(trail_across / (TRAIL_WIDTH * 0.8), leftover_y * 2.0);
    float leftover = smoothstep(1.0, 0.6, length(bead)) * step(hash(leftover_cell), LEFTOVER_SHARE) * behind;
    vec3 wet = leftover > trail ? vec3(bead, leftover) : vec3(trail_across / TRAIL_WIDTH, 0.0, trail);

    vec3 picked = drop.z >= wet.z ? drop : wet;
    picked.z *= shown;
    return picked;
}

void fragment() {
    vec3 small = beads(SCREEN_UV, TIME);
    vec3 large = runners(SCREEN_UV, TIME);
    vec3 drop = large.z > small.z ? large : small;
    float cover = drop.z * strength;
    vec2 normal = drop.xy;
    vec3 seen = textureLod(screen_texture, SCREEN_UV - normal * bend * cover, cover).rgb;
    float edge = smoothstep(0.5, 1.0, length(normal)) * cover;
    float shine = smoothstep(0.4, 0.0, length(normal - SHINE_AT)) * cover;
    COLOR = vec4(seen * (1.0 + cover * WATER_LIGHT - edge * EDGE_SHADE) + vec3(shine * SHINE), 1.0);
}
