// Datos y calculo de los anillos del diseño aprobado (Main.dc.html, bloque DCLogic).
// Todo esto corre en build time: Astro lo resuelve a HTML estatico, no hay JS de cliente.

const RING_BIG_CIRCUMFERENCE = 2 * Math.PI * 26;
const RING_SMALL_CIRCUMFERENCE = 2 * Math.PI * 11;

export const ICONS = {
  mouse: 'M12 3a6 6 0 0 0-6 6v6a6 6 0 0 0 12 0V9a6 6 0 0 0-6-6zM12 7v4',
  headset:
    'M4 17v-5a8 8 0 0 1 16 0v5M20 17.5a2 2 0 0 1-2 2h-.5a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2H20zM4 17.5a2 2 0 0 0 2 2h.5a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H4z',
  gamepad:
    'M7 11h4M9 9v4M15 12h.01M17.5 10h.01M17 6H7a4 4 0 0 0-4 3.6l-.6 5.8A2.8 2.8 0 0 0 5.2 18.5c.9 0 1.7-.5 2.2-1.2L8.5 15.5h7l1.1 1.8c.5.7 1.3 1.2 2.2 1.2a2.8 2.8 0 0 0 2.8-3.1L21 9.6A4 4 0 0 0 17 6z',
} as const;

interface RawDevice {
  name: string;
  kind: string;
  pct: number;
  icon: string;
  low?: boolean;
  charging?: boolean;
}

export interface Device {
  name: string;
  kind: string;
  pct: number;
  icon: string;
  low: boolean;
  charging: boolean;
  hasState: boolean;
  state: string;
  chipBg: string;
  chipInk: string;
  ring: string;
  dash: string;
  dashSmall: string;
  aria: string;
}

const rawDevices: RawDevice[] = [
  { name: 'Razer Cobra HyperSpeed', kind: 'Mouse', pct: 82, icon: ICONS.mouse },
  { name: 'Arctis Nova 5', kind: 'Auriculares', pct: 15, low: true, icon: ICONS.headset },
  { name: 'Control Xbox', kind: 'Joystick', pct: 64, charging: true, icon: ICONS.gamepad },
];

export const devices: Device[] = rawDevices.map((d) => {
  const low = !!d.low;
  const charging = !!d.charging;
  return {
    name: d.name,
    kind: d.kind,
    pct: d.pct,
    icon: d.icon,
    low,
    charging,
    hasState: low || charging,
    state: low ? 'Batería baja' : charging ? 'Cargando' : '',
    chipBg: low ? 'var(--warning-soft)' : 'var(--info-soft)',
    chipInk: low ? 'var(--warning)' : 'var(--info)',
    ring: low ? 'var(--warning)' : 'var(--brand-ink)',
    dash: `${(RING_BIG_CIRCUMFERENCE * d.pct / 100).toFixed(1)} ${RING_BIG_CIRCUMFERENCE.toFixed(1)}`,
    dashSmall: `${(RING_SMALL_CIRCUMFERENCE * d.pct / 100).toFixed(1)} ${RING_SMALL_CIRCUMFERENCE.toFixed(1)}`,
    aria: `${d.name}, ${d.pct} por ciento${low ? ', batería baja' : ''}${charging ? ', cargando' : ''}`,
  };
});

export interface Feature {
  title: string;
  body: string;
  icon: string;
}

export const features: Feature[] = [
  {
    title: 'Un ícono por dispositivo',
    body: 'Cada uno con su anillo y su número. O uno solo con el más bajo, y fijás los que querés ver siempre.',
    icon: 'M3 6h18M3 12h18M3 18h18',
  },
  {
    title: 'Aviso antes de quedarte sin',
    body: 'Te avisa una vez cuando un dispositivo baja de su umbral. Cada uno tiene el suyo.',
    icon: 'M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.9 1.9 0 0 0 3.4 0',
  },
  {
    title: 'Se nota cuando carga',
    body: 'El ícono lo muestra con forma y con palabra, no solo con color.',
    icon: 'M13 2 4 14h7l-1 8 9-12h-7z',
  },
  {
    title: 'Se actualiza sola',
    body: 'Baja la versión nueva en segundo plano y la aplica cuando cerrás. Nada de instaladores cada mes.',
    icon: 'M21 12a9 9 0 1 1-3-6.7L21 8M21 3v5h-5',
  },
  {
    title: 'Claro, oscuro y alto contraste',
    body: 'Sigue el tema de Windows y el de la barra de tareas, y respeta el modo de alto contraste.',
    icon: 'M12 3a9 9 0 1 0 9 9 7 7 0 0 1-9-9z',
  },
  {
    title: 'Si no aparece, te dice por qué',
    body: 'Un diagnóstico que muestra qué ve Windows de tus dispositivos, para que sepas qué falta.',
    icon: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM21 21l-5-5',
  },
];

export interface Supported {
  name: string;
  via: string;
}

export const supported: Supported[] = [
  { name: 'Razer Cobra HyperSpeed', via: 'HID de Razer' },
  { name: 'SteelSeries Arctis Nova 5', via: 'HID de SteelSeries' },
  { name: 'Controles de Xbox', via: 'XInput' },
  { name: 'Bluetooth que informan batería', via: 'Bluetooth de Windows' },
];
