import { useState } from "react";

import {
  createCatalogItem,
  getProductOptions,
  searchCatalog,
} from "./api/catalog";

function itemDetails(item) {
  return [item.brand ?? "Any brand", item.size].filter(Boolean).join(" · ");
}

// Lets a shopper add an item the catalog is missing by picking a real
// product from a retailer's catalog. No location is needed: the server
// searches every retailer and builds the item from the product; shoppers
// only pick an option.
function CatalogSearch({ query, onItemCreated }) {
  const [results, setResults] = useState([]);
  const [failedRetailers, setFailedRetailers] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  const [searchedQuery, setSearchedQuery] = useState("");
  const [picked, setPicked] = useState(null);
  const [options, setOptions] = useState(null);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState("");

  const trimmedQuery = query.trim();

  if (trimmedQuery.length < 2) {
    return null;
  }

  async function handleSearch() {
    setIsSearching(true);
    setError("");
    setPicked(null);
    setOptions(null);
    setResults([]);
    setFailedRetailers([]);

    try {
      const result = await searchCatalog(trimmedQuery);

      // Each product remembers the store it was found at; its options
      // and creation are looked up at that same store.
      setResults(
        result.retailers.flatMap((retailer) =>
          retailer.products.map((product) => ({
            ...product,
            retailerName: retailer.retailerName,
            storeLocationId: retailer.storeLocationId,
          }))
        )
      );
      setFailedRetailers(result.failedRetailers);
      setSearchedQuery(trimmedQuery);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsSearching(false);
    }
  }

  async function handlePick(product) {
    setPicked(product);
    setOptions(null);
    setError("");
    setIsBusy(true);

    try {
      setOptions(await getProductOptions(product.storeLocationId, product.externalProductId));
    } catch (err) {
      setError(err.message);
    } finally {
      setIsBusy(false);
    }
  }

  function finish() {
    setPicked(null);
    setOptions(null);
    setResults([]);
    setSearchedQuery("");
  }

  async function handleUseExisting(item) {
    finish();
    await onItemCreated(item, false);
  }

  async function handleCreate(kind) {
    setIsBusy(true);
    setError("");

    try {
      const result = await createCatalogItem(
        picked.storeLocationId,
        picked.externalProductId,
        kind
      );
      finish();
      await onItemCreated(result.item, result.created);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsBusy(false);
    }
  }

  const hasChoices =
    options && (options.existingItems.length > 0 || options.newItems.length > 0);

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

      {error && !picked && (
        <p className="alert alert-error" role="alert">
          {error}
        </p>
      )}

      {picked ? (
        <div className="catalog-options">
          <p>
            <strong>{picked.name}</strong> ({picked.retailerName}, {picked.size})
          </p>

          {isBusy && !options && <p role="status">Checking the catalog...</p>}

          {options?.existingItems.length > 0 && (
            <section aria-label="Already in the catalog">
              <h3>Already in the catalog</h3>
              <p className="catalog-hint">This product already counts as:</p>
              <ul className="catalog-results">
                {options.existingItems.map((item) => (
                  <li key={item.id} className="item-card">
                    <div>
                      <h3>{item.name}</h3>
                      <p>{itemDetails(item)}</p>
                    </div>
                    <button
                      className="button button-primary"
                      type="button"
                      onClick={() => handleUseExisting(item)}
                      disabled={isBusy}
                    >
                      Add to my list
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {options?.newItems.length > 0 && (
            <section aria-label="Add a new item">
              <h3>Add a new item</h3>
              <ul className="catalog-results">
                {options.newItems.map((option) => (
                  <li key={option.kind} className="item-card">
                    <div>
                      <h3>{option.name}</h3>
                      <p>
                        {option.kind === "Branded"
                          ? `Only ${option.brand} · ${option.size}`
                          : `Any brand · ${option.size}`}
                      </p>
                    </div>
                    <button
                      className="button button-secondary"
                      type="button"
                      onClick={() => handleCreate(option.kind)}
                      disabled={isBusy}
                    >
                      {option.kind === "Branded" ? `Add as ${option.brand}` : "Add as any brand"}
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {options && !hasChoices && (
            <p className="empty-state">
              This product can't be added automatically. Try a more specific product.
            </p>
          )}

          {error && (
            <p className="alert alert-error" role="alert">
              {error}
            </p>
          )}

          <button className="button button-link" type="button" onClick={() => setPicked(null)}>
            Back to results
          </button>
        </div>
      ) : (
        results.length > 0 && (
          <ul className="catalog-results" aria-label="Store products">
            {results.map((product) => (
              <li key={`${product.storeLocationId}-${product.externalProductId}`} className="item-card">
                <div>
                  <h3>{product.name}</h3>
                  <p>
                    {product.retailerName} · {product.size}
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
