export default function mountGeoLocation(): boolean {
    // Only check the API exists — actually calling getCurrentPosition makes real browsers run a
    // permission/geolocation-service lookup on every crawled page, which stalls the headless backends.
    return typeof navigator.geolocation?.getCurrentPosition === 'function';
}