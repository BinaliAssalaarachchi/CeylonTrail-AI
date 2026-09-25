import { useEffect } from 'react'

export default function Modal({ title, labelledBy, children, onClose, className = '' }) {
  useEffect(() => {
    function handleKeyDown(event) { if (event.key === 'Escape') onClose() }
    document.addEventListener('keydown', handleKeyDown)
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.removeEventListener('keydown', handleKeyDown); document.body.style.overflow = previousOverflow }
  }, [onClose])

  return <div className="app-modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}><section className={`app-modal ${className}`} role="dialog" aria-modal="true" aria-labelledby={labelledBy || undefined}><div className="app-modal-heading"><h2 id={labelledBy}>{title}</h2><button className="modal-close" type="button" onClick={onClose} aria-label="Close dialog">×</button></div>{children}</section></div>
}
