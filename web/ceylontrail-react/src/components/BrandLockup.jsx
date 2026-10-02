function BrandMark({ light = false, size = 44 }) {
  const color = light ? '#ffffff' : 'var(--color-forest)'
  const accent = light ? 'rgba(255,255,255,.58)' : 'var(--color-tea)'
  return <svg className="current-brand-mark" width={size} height={size} viewBox="0 0 80 80" role="img" aria-label="CeylonTrail mark">
    <circle cx="56" cy="23" r="8" fill={color} opacity=".9" />
    <path d="M11 50c13-8 28-8 43-1 7 3 13 3 17 1" fill="none" stroke={color} strokeWidth="3" strokeLinecap="round" />
    <path d="M16 63c9-8 19-8 28-3 8 5 14 4 20-4" fill="none" stroke={color} strokeWidth="5" strokeLinecap="round" />
    <path d="M25 29c-7 5-9 13-7 20" fill="none" stroke={accent} strokeWidth="2" strokeLinecap="round" />
    <circle cx="56" cy="23" r="3" fill={color} />
  </svg>
}

export default function BrandLockup({ light = false, compact = false }) {
  return <div className={`current-brand-lockup ${light ? 'current-brand-lockup-light' : ''}`}>
    <BrandMark light={light} size={compact ? 38 : 48} />
    <span className="current-brand-copy"><strong>CeylonTrail</strong>{!compact && <small>Tourism Operations</small>}</span>
  </div>
}

export { BrandMark }
