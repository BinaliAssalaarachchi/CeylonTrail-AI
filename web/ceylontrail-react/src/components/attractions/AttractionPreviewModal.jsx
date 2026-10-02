import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import Modal from '../Modal'
import StatusBadge from './StatusBadge'
import { attractionGalleryFor } from '../../utils/attractionImages'

function imageFor(attraction) { return attraction.images?.find((image) => image.isPrimary && image.imageUrl) || attraction.images?.find((image) => image.imageUrl) }

export default function AttractionPreviewModal({ attraction, onClose, canEdit = false }) {
  const apiImages = attraction.images?.filter((image) => image.imageUrl) || []
  const mappedImages = attractionGalleryFor(attraction.name).map((imageUrl, index) => ({ id: `mapped-${index}`, imageUrl, altText: attraction.name, sortOrder: index }))
  const images = apiImages.length ? apiImages : mappedImages
  const [selectedImage, setSelectedImage] = useState(imageFor(attraction) || images[0])
  const close = useCallback(() => onClose(), [onClose])
  const currentImage = selectedImage || images[0]
  return <Modal title={attraction.name} labelledBy="attraction-preview-title" onClose={close} className="attraction-preview-modal"><div className="preview-image-stage">{currentImage ? <img src={currentImage.imageUrl} alt={currentImage.altText || attraction.name} /> : <span aria-hidden="true">✦</span>}</div>{images.length > 1 && <div className="preview-gallery" aria-label="Attraction images">{images.map((image) => <button className={currentImage?.id === image.id ? 'selected' : ''} type="button" key={image.id} onClick={() => setSelectedImage(image)} aria-label={`Preview image ${image.sortOrder + 1}`}><img src={image.imageUrl} alt="" /></button>)}</div>}<div className="preview-facts"><div><span>Category</span><strong>{attraction.category?.name || 'Uncategorised'}</strong></div><div><span>Status</span><StatusBadge status={attraction.status} isActive={attraction.isActive} /></div><div><span>District</span><strong>{attraction.district}</strong></div><div><span>Price</span><strong>LKR {attraction.price}</strong></div></div><p className="preview-description">{attraction.description}</p><div className="modal-actions">{canEdit && <Link className="button button-primary" to={`/provider/attractions/${attraction.id}/edit`} onClick={onClose}>Edit attraction</Link>}<button className="button button-secondary-light" type="button" onClick={onClose}>Close</button></div></Modal>
}
