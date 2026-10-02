const attractionImageSets = {
  'Arugam Bay Coastal Surf Lesson': ['/images/destinations/arugam_bay/01.jpg', '/images/destinations/arugam_bay/02.jpg', '/images/destinations/arugam_bay/03.jpg'],
  'Bentota River Kayak': ['/images/destinations/bentota/01.jpg', '/images/destinations/bentota/02.jpg', '/images/destinations/bentota/03.jpg'],
  'Ella Tea Country Hike': ['/images/destinations/ella/01.jpg', '/images/destinations/ella/02.jpg', '/images/destinations/ella/03.jpg'],
  'Galle Fort Heritage Walk': ['/images/destinations/galle_fort/01.jpg', '/images/destinations/galle_fort/02.jpg', '/images/destinations/galle_fort/03.jpg'],
  'Kandy Lake and Temple Walk': ['/images/destinations/kandy/01.jpg', '/images/destinations/kandy/02.jpg', '/images/destinations/kandy/03.jpg'],
  'Sigiriya Heritage Sunrise Trail': ['/images/destinations/sigiriya/01.jpg', '/images/destinations/sigiriya/02.jpg', '/images/destinations/sigiriya/03.jpg'],
  'Sinharaja Rainforest Nature Walk': ['/images/destinations/sinharaja/01.jpg', '/images/destinations/sinharaja/02.jpg', '/images/destinations/sinharaja/03.jpg'],
  'Udawalawe Wildlife Safari': ['/images/destinations/udawalawe/01.jpg', '/images/destinations/udawalawe/02.jpg', '/images/destinations/udawalawe/03.jpg'],
}

export function attractionGalleryFor(name) {
  return attractionImageSets[name] || []
}

export function primaryAttractionImageFor(name) {
  return attractionGalleryFor(name)[0] || null
}

export { attractionImageSets }
