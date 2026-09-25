import Modal from './Modal'

export default function ConfirmationModal({ title, message, confirmLabel = 'Confirm', cancelLabel = 'Cancel', isLoading = false, error = '', onConfirm, onClose }) {
  return <Modal title={title} labelledBy="confirmation-modal-title" onClose={isLoading ? () => {} : onClose} className="confirmation-modal"><p className="confirmation-message">{message}</p>{error && <p className="form-error" role="alert">{error}</p>}<div className="modal-actions"><button className="button button-secondary-light" type="button" onClick={onClose} disabled={isLoading}>{cancelLabel}</button><button className="button button-danger" type="button" onClick={onConfirm} disabled={isLoading}>{isLoading ? 'Working…' : confirmLabel}</button></div></Modal>
}
