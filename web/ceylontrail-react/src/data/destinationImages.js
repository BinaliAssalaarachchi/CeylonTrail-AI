export const destinationImages = {
  arugamBay: ['/images/destinations/arugam_bay/01.jpg', '/images/destinations/arugam_bay/02.jpg', '/images/destinations/arugam_bay/03.jpg'],
  bentota: ['/images/destinations/bentota/01.jpg', '/images/destinations/bentota/02.jpg', '/images/destinations/bentota/03.jpg'],
  ella: ['/images/destinations/ella/01.jpg', '/images/destinations/ella/02.jpg', '/images/destinations/ella/03.jpg'],
  galleFort: ['/images/destinations/galle_fort/01.jpg', '/images/destinations/galle_fort/02.jpg', '/images/destinations/galle_fort/03.jpg'],
  kandy: ['/images/destinations/kandy/01.jpg', '/images/destinations/kandy/02.jpg', '/images/destinations/kandy/03.jpg'],
  sigiriya: ['/images/destinations/sigiriya/01.jpg', '/images/destinations/sigiriya/02.jpg', '/images/destinations/sigiriya/03.jpg'],
  sinharaja: ['/images/destinations/sinharaja/01.jpg', '/images/destinations/sinharaja/02.jpg', '/images/destinations/sinharaja/03.jpg'],
  udawalawe: ['/images/destinations/udawalawe/01.jpg', '/images/destinations/udawalawe/02.jpg', '/images/destinations/udawalawe/03.jpg'],
}

export const loginSlides = [
  ...[
    ['Arugam Bay', 'East Coast', 'arugam_bay'],
    ['Bentota', 'Southern Coast', 'bentota'],
    ['Ella', 'Highlands', 'ella'],
    ['Galle Fort', 'Southern Coast', 'galle_fort'],
    ['Kandy', 'Central Highlands', 'kandy'],
    ['Sigiriya', 'Cultural Triangle', 'sigiriya'],
    ['Sinharaja', 'Rainforest', 'sinharaja'],
    ['Udawalawe', 'Wildlife', 'udawalawe'],
  ].flatMap(([destination, region, folder]) => [1, 2, 3].map((number) => ({
    destination,
    region,
    image: `/images/login/${folder}-${String(number).padStart(2, '0')}.jpg`,
    position: 'center',
  }))),
]
