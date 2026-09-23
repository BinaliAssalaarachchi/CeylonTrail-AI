export function toAttractionForm(attraction) {
  if (!attraction) return { categoryId: '', name: '', description: '', district: '', address: '', latitude: '', longitude: '', price: '' }
  return {
    categoryId: attraction.categoryId || '', name: attraction.name || '', description: attraction.description || '',
    district: attraction.district || '', address: attraction.address || '', latitude: attraction.latitude ?? '',
    longitude: attraction.longitude ?? '', price: attraction.price ?? '',
  }
}
