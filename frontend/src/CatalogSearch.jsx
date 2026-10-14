import { useState } from "react";

import { createCatalogItem, searchCatalog } from "./api/catalog";

// Lets a shopper add an item the catalog is missing by picking a real
// product from a nearby store. One store per retailer is searched.
function CatalogSearch({ query, stores, onItemCreated }) {
  const [results, setResults] = useState([]);
  const [failedRetailers, setFailedRetailers] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  const [searchedQuery, setSearchedQuery] = useState("");
  const [picked, setPicked] = useState(null);
  const [draft, setDraft] = useState({ name: "", size: "", category: "" });
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState("");

  const trimmedQuery = query.trim();
  // Stores arrive nearest first; search the nearest one of each retailer.
  const storePerRetailer = stores.filter(
    (store, index) =>
      stores.findIndex((other) => other.retailerId === store.retailerId) === index
  );

  if (trimmedQuery.length < 2) {
    return null;
  }

  if (storePerRetailer.length === 0) {
    return (
      <p className="catalog-hint">
        Can't find it? Find nearby stores in Step 2 to search their products.
      </p>
    );
  }

  async function handleSearch() {
    setIsSearching(true);
    setError("");
    setPicked(null);
    setResults([]);
    setFailedRetailers([]);

    const settled = await Promise.allSettled(
      storePerRetailer.map((store) => searchCatalog(store.id, trimmedQuery))
    );

    const found = [];
    const failed = [];
    settled.forEach((outcome, index) => {
      const store = storePerRetailer[index];
      if (outcome.status === "fulfilled") {
        outcome.value.products.forEach((product) =>
          found.push({ ...product, store, retailerName: outcome.value.retailerName })
        );
      } else {
        failed.push(store.retailerName ?? store.name);
      }
    });

    setResults(found);
    setFailedRetailers(failed);
    setSearchedQuery(trimmedQuery);
    setIsSearching(false);
  }

  function handlePick(product) {
    setPicked(product);
    setError("");
    setDraft({ name: product.suggestedName, size: product.size, category: "" });
  }

  async function handleCreate(event) {
    event.preventDefault();
    setIsSaving(true);
    setError("");

    try {
      const result = await createCatalogItem({
        storeLocationId: picked.store.id,
        externalProductId: picked.externalProductId,
        name: draft.name,
        size: draft.size,
        category: draft.category || null,
      });

      setPicked(null);
      setResults([]);
      setSearchedQuery("");
      await onItemCreated(result.item, result.created);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="catalog-search">
      <button
        className="button button-secondary"
        type="button"
        onClick={handleSearch}
        disabled={isSearching}
      >
        {isSearching ? "Searching stores..." : `Search stores for "${trimmedQuery}"`}
      </button>

      {failedRetailers.length > 0 && (
        <p className="alert alert-warning" role="status">
          Could not search: {failedRetailers.join(", ")}.
        </p>
      )}

      {searchedQuery && !isSearching && results.length === 0 && failedRetailers.length === 0 && (
        <p className="empty-state">No store products found for "{searchedQuery}".</p>
      )}

      {picked ? (
        <form className="catalog-form" onSubmit={handleCreate}>
          <p>
            New item based on <strong>{picked.name}</strong> ({picked.retailerName}). Edit the
            name to drop the brand; it must still describe this product.
          </p>

          <label className="retailer-field">
            <span>Item name</span>
            <input
              value={draft.name}
              onChange={(event) => setDraft({ ...draft, name: event.target.value })}
              required
              minLength={2}
              maxLength={100}
            />
          </label>

          <label className="retailer-field">
            <span>Size</span>
            <input
              value={draft.size}
              onChange={(event) => setDraft({ ...draft, size: event.target.value })}
              required
              maxLength={50}
            />
          </label>

          <label className="retailer-field">
            <span>Category (optional)</span>
            <input
              value={draft.category}
              onChange={(event) => setDraft({ ...draft, category: event.target.value })}
              maxLength={50}
              placeholder="Pantry"
            />
          </label>

          {error && (
            <p className="alert alert-error" role="alert">
              {error}
            </p>
          )}

          <div className="catalog-form-actions">
            <button className="button button-primary" type="submit" disabled={isSaving}>
              {isSaving ? "Adding..." : "Add item to my list"}
            </button>
            <button className="button button-link" type="button" onClick={() => setPicked(null)}>
              Back to results
            </button>
          </div>
        </form>
      ) : (
        results.length > 0 && (
          <ul className="catalog-results" aria-label="Store products">
            {results.map((product) => (
              <li key={`${product.store.id}-${product.externalProductId}`} className="item-card">
                <div>
                  <h3>{product.name}</h3>
                  <p>
                    {product.retailerName} · {product.size}
                    {product.shelfPrice != null && ` · $${product.shelfPrice.toFixed(2)}`}
                  </p>
                </div>
                <button
                  className="button button-secondary"
                  type="button"
                  onClick={() => handlePick(product)}
                >
                  Use this
                </button>
              </li>
            ))}
          </ul>
        )
      )}
    </div>
  );
}

export default CatalogSearch;
