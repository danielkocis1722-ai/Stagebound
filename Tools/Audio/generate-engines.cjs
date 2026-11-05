// Original automotive synthesis at 88.2 kHz, EQ'd and downsampled to 44.1 kHz.
// node Tools/Audio/generate-engines.cjs [AE86|R32|RX7]
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const rate = 44100;
const renderRate = rate * 2;
const bands = [[900, 'Idle'], [1800, 'Low'], [3000, 'Low'], [4500, 'Mid'], [6000, 'High']];
const vehicles = [
    { name: 'AE86', pulses: 2, seed: 86, rolloff: .065,
        resonances: [[220, .6, .5], [1050, .42, 1], [2600, .35, .85]],
        intake: 1950, intakeGain: .23, mechanical: .065, exhaustGain: .72, saturation: 1.35 },
    { name: 'R32', pulses: 3, seed: 32, rolloff: .15,
        resonances: [[155, .8, 1.5], [420, .65, .95], [1250, .42, .4]],
        intake: 1100, intakeGain: .10, mechanical: .16, exhaustGain: .85, saturation: 1.15, turbo: true },
    { name: 'RX7', pulses: 2, seed: 13, rolloff: .095,
        resonances: [[650, .6, .6], [1700, .55, 1.2], [3400, .4, .85]],
        intake: 2900, intakeGain: .15, mechanical: .018, exhaustGain: .67, saturation: 1.2, rotary: true }
];

class Biquad {
    constructor(kind, frequency, q = .707) {
        const w = 2 * Math.PI * frequency / renderRate;
        const alpha = Math.sin(w) / (2 * q);
        const cosine = Math.cos(w);
        const a0 = 1 + alpha;
        const sign = kind === 'highpass' ? 1 : -1;
        this.b0 = (1 + sign * cosine) / (2 * a0);
        this.b1 = -sign * (1 + sign * cosine) / a0;
        this.b2 = this.b0;
        if (kind === 'bandpass') {
            this.b0 = alpha / a0; this.b1 = 0; this.b2 = -alpha / a0;
        }
        this.a1 = -2 * cosine / a0; this.a2 = (1 - alpha) / a0;
        this.z1 = 0; this.z2 = 0;
    }
    next(x) {
        const y = this.b0 * x + this.z1;
        this.z1 = this.b1 * x - this.a1 * y + this.z2;
        this.z2 = this.b2 * x - this.a2 * y;
        return y;
    }
}

function resonance(frequency, center, width) {
    return Math.exp(-.5 * (Math.log(frequency / center) / width) ** 2);
}

function harmonics(vehicle, rpm, loaded) {
    const firing = rpm / 60 * vehicle.pulses;
    const spectrum = [];
    for (let harmonic = 1; harmonic <= 64 && harmonic * firing < 14500; harmonic++) {
        const frequency = harmonic * firing;
        const body = vehicle.resonances.reduce((sum, [center, width, gain]) =>
            sum + gain * resonance(frequency, center, width), .10);
        const rolloff = vehicle.rolloff + (loaded ? 0 : .085);
        let amplitude = Math.exp(-harmonic * rolloff) * body / Math.sqrt(harmonic);
        // Rotary overlap emphasizes upper orders. The six-cylinder retains its low exhaust orders.
        if (harmonic === 1) amplitude *= vehicle.rotary ? .23 : vehicle.turbo ? .9 : .45;
        if (vehicle.rotary && harmonic > 1) amplitude *= 1 + .35 * Math.sin(harmonic * .8);
        const intake = resonance(frequency, vehicle.intake + rpm * .08, .6) *
            Math.exp(-harmonic * .065) / Math.sqrt(harmonic);
        spectrum.push({ harmonic, exhaust: amplitude * vehicle.exhaustGain,
            intake: intake * vehicle.intakeGain * (loaded ? 2.1 : .35),
            phase: -.27 * harmonic - .035 * harmonic * harmonic });
    }
    return spectrum;
}

function renderPair(vehicle, rpm) {
    const firing = rpm / 60 * vehicle.pulses;
    const length = rate * 4;
    const overlap = Math.round(rate * .16);
    const warmup = Math.round(renderRate * .4);
    const frames = (length + overlap) * 2;
    const coast = new Float64Array(length + overlap);
    const loaded = new Float64Array(length + overlap);
    const spectra = [harmonics(vehicle, rpm, false), harmonics(vehicle, rpm, true)];
    const noiseIntake = new Biquad('bandpass', vehicle.intake + rpm * .08, .8);
    const noiseExhaust = new Biquad('lowpass', vehicle.rotary ? 1700 : vehicle.turbo ? 700 : 1100);
    const turboNoise = new Biquad('bandpass', 3800, .65);
    const filters = [false, true].map(onLoad => ({
        highpass: new Biquad('highpass', vehicle.rotary ? 70 : 38),
        lowpass: new Biquad('lowpass', onLoad ? 14000 : 8000),
        antiAlias: new Biquad('lowpass', 15500)
    }));
    let seed = vehicle.seed;
    const loadAmount = Math.min(1, rpm / 6500);
    for (let sample = -warmup; sample < frames; sample++) {
        const t = sample / renderRate;
        // Gentle periodic cycle variation preserves firing rhythm and loop continuity.
        const jitter = (Math.sin(t * Math.PI * 6) + .3 * Math.sin(t * Math.PI * 14)) *
            (vehicle.rotary ? .001 : .003) * (1 - loadAmount * .7);
        const phase = 2 * Math.PI * (firing * t + jitter);
        seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
        const noise = seed / 2147483648 - 1;
        const intakeAir = noiseIntake.next(noise);
        const exhaustAir = noiseExhaust.next(noise);
        const turboAir = turboNoise.next(noise);
        const breath = .48 + .25 * Math.sin(phase - .7) + .18 * Math.sin(phase * 2);
        const crank = 2 * Math.PI * rpm / 60 * t;
        const mechanical = vehicle.mechanical * (Math.sin(crank) + .3 * Math.sin(crank * 2 + .4));
        const rotaryWhine = vehicle.rotary ? .07 * Math.sin(crank * 6 + .08 * Math.sin(crank / 3)) : 0;
        const turbo = vehicle.turbo ? loadAmount ** 1.8 *
            (.09 * turboAir + .012 * Math.sin(2 * Math.PI * Math.round((1250 + rpm * .14) * 4) / 4 * t)) : 0;
        for (let mode = 0; mode < 2; mode++) {
            let exhaust = 0, intake = 0;
            for (const partial of spectra[mode]) {
                exhaust += partial.exhaust * Math.sin(phase * partial.harmonic + partial.phase);
                intake += partial.intake * Math.sin(phase * partial.harmonic + partial.phase + 1.1);
            }
            const onLoad = mode === 1;
            let value = exhaust + intake + mechanical + rotaryWhine;
            value += breath * (intakeAir * (onLoad ? .30 : .055) + exhaustAir * (onLoad ? .13 : .07));
            if (onLoad) value += turbo;
            const filter = filters[mode];
            value = filter.highpass.next(value);
            value = Math.tanh(value * (onLoad ? vehicle.saturation : vehicle.saturation * .75));
            value = filter.antiAlias.next(filter.lowpass.next(value));
            if (sample >= 0 && sample % 2 === 0) (onLoad ? loaded : coast)[sample / 2] = value;
        }
    }
    return [coast, loaded].map(samples => {
        const loop = samples.slice(0, length);
        for (let i = 0; i < overlap; i++) {
            const fade = .5 - .5 * Math.cos(Math.PI * i / (overlap - 1));
            loop[i] = samples[length + i] * (1 - fade) + samples[i] * fade;
        }
        const mean = loop.reduce((sum, x) => sum + x, 0) / length;
        const rms = Math.sqrt(loop.reduce((sum, x) => sum + (x - mean) ** 2, 0) / length);
        const peak = loop.reduce((max, x) => Math.max(max, Math.abs(x - mean)), 0);
        const gain = Math.min(.20 / rms, .68 / peak);
        return loop.map(x => (x - mean) * gain);
    });
}

function writeWave(file, samples) {
    const buffer = Buffer.alloc(44 + samples.length * 2);
    buffer.write('RIFF', 0); buffer.writeUInt32LE(buffer.length - 8, 4);
    buffer.write('WAVEfmt ', 8); buffer.writeUInt32LE(16, 16);
    buffer.writeUInt16LE(1, 20); buffer.writeUInt16LE(1, 22);
    buffer.writeUInt32LE(rate, 24); buffer.writeUInt32LE(rate * 2, 28);
    buffer.writeUInt16LE(2, 32); buffer.writeUInt16LE(16, 34);
    buffer.write('data', 36); buffer.writeUInt32LE(samples.length * 2, 40);
    samples.forEach((x, i) => buffer.writeInt16LE(Math.round(x * 32767), 44 + i * 2));
    fs.writeFileSync(file, buffer);
}

function generate(only) {
    for (const vehicle of vehicles.filter(v => !only || v.name === only)) {
        const folder = path.join(root, 'Assets/Audio/Vehicles', vehicle.name);
        fs.mkdirSync(folder, { recursive: true });
        for (const [rpm, band] of bands) {
            renderPair(vehicle, rpm).forEach((samples, i) =>
                writeWave(path.join(folder, vehicle.name + '_' + band + '_' + rpm + (i ? '_Load' : '') + '.wav'), samples));
            console.log(vehicle.name + ' ' + rpm + ' RPM coast/load rendered');
        }
    }
}

module.exports = { generate };
if (require.main === module) generate(process.argv[2]);
