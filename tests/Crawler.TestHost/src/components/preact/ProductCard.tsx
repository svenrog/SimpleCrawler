import type { Product } from '../../shared/catalog';

function Stars({ rating }: { rating: number }) {
    const full = Math.floor(rating);
    return (
        <span className="stars" aria-label={`${rating} out of 5`}>
            {Array.from({ length: 5 }, (_, i) => (
                <span key={i} className={i < full ? 'star star--on' : 'star'}>
                    ★
                </span>
            ))}
        </span>
    );
}

export default function ProductCard({ product }: { product: Product }) {
    const discounted = product.price < product.listPrice;
    return (
        <article className="card" data-sku={product.id} data-in-stock={product.inStock}>
            <div className="card__media">
                <div className="card__thumb" style={{ backgroundColor: '#0b5' }} aria-hidden="true" />
                {discounted && <span className="card__badge">Sale</span>}
                {!product.inStock && <span className="card__badge card__badge--muted">Backorder</span>}
            </div>
            <div className="card__body">
                <p className="card__brand">{product.brand}</p>
                <h3 className="card__title">
                    <a href={`#${product.id}`}>{product.name}</a>
                </h3>
                <div className="card__meta">
                    <Stars rating={product.rating} />
                    <span className="card__reviews">({product.reviews})</span>
                </div>
                <p className="card__blurb">{product.blurb}</p>
                <ul className="card__tags">
                    {product.tags.map((tag) => (
                        <li key={tag} className="tag">
                            {tag}
                        </li>
                    ))}
                </ul>
                <div className="card__footer">
                    <span className="card__price">
                        {discounted && <s className="card__list-price">{product.listPrice} kr</s>}
                        <strong>{product.price} kr</strong>
                    </span>
                    <button type="button" className="card__buy" disabled={!product.inStock}>
                        {product.inStock ? 'Add to cart' : 'Notify me'}
                    </button>
                </div>
            </div>
        </article>
    );
}
