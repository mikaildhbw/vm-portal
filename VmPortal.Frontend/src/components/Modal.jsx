// Einfacher, abhängigkeitsfreier Modal-Wrapper (kantig, kein Icon-Set im Projekt) -
// schließt bei Klick auf das Overlay oder den Schließen-Button.
function Modal({ title, onClose, children }) {
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button type="button" className="secondary" onClick={onClose}>
            Schließen
          </button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  )
}

export default Modal
