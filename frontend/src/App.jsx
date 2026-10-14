import {
  useEffect,
  useRef,
  useState,
} from "react";

import {
  getRetailers,
} from "./api/retailers";

import {
  getItems,
} from "./api/items";

import {
  login,
  register,
  logout,
  isLoggedIn,
} from "./api/auth";

import {
  getGroceryList,
  setGroceryListQuantity,
  removeGroceryListItem,
} from "./api/groceryList";

import {
  searchNearbyStores,
} from "./api/stores";

import {
  compareStores,
} from "./api/comparisons";

import CatalogSearch from "./CatalogSearch";

import "./App.css";

function App() {
  const [retailers, setRetailers] =
    useState([]);

  const [items, setItems] =
    useState([]);
  const [itemQuery, setItemQuery] = useState("");

  const [groceryList, setGroceryList] =
    useState([]);

  const [stores, setStores] =
    useState([]);
  const [postalCode, setPostalCode] = useState("");
  const [searchedPostalCode, setSearchedPostalCode] = useState("");
  const [failedRetailers, setFailedRetailers] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  const searchSequence = useRef(0);

  const [
    comparisonResult,
    setComparisonResult,
  ] = useState(null);

  const [loggedIn, setLoggedIn] =
    useState(isLoggedIn());

  const [authMode, setAuthMode] =
    useState("login");

  const [firstName, setFirstName] =
    useState("");

  const [lastName, setLastName] =
    useState("");

  const [email, setEmail] =
    useState("");

  const [password, setPassword] =
    useState("");

  const [quantities, setQuantities] =
    useState({});

  const [
    selectedRetailerId,
    setSelectedRetailerId,
  ] = useState("");

  const [
    selectedStoreIds,
    setSelectedStoreIds,
  ] = useState([]);

  const [message, setMessage] =
    useState("");

  const [error, setError] =
    useState("");

  const [isComparing, setIsComparing] =
    useState(false);

  useEffect(() => {
    async function loadInitialData() {
      try {
        const [
          retailerData,
          itemData,
        ] = await Promise.all([
          getRetailers(),
          getItems(),
        ]);

        setRetailers(retailerData);
        setItems(itemData);
      } catch (err) {
        setError(err.message);
      }
    }

    loadInitialData();
  }, []);

  useEffect(() => {
    if (!loggedIn) {
      return;
    }

    refreshGroceryList();
  }, [loggedIn]);

  async function refreshGroceryList() {
    try {
      const data =
        await getGroceryList();

      setGroceryList(data);
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleAuthSubmit(
    event
  ) {
    event.preventDefault();

    setError("");
    setMessage("");

    try {
      if (authMode === "register") {
        await register(
          firstName,
          lastName,
          email,
          password
        );

        setMessage(
          "Registration succeeded. You can now log in."
        );

        setAuthMode("login");
        setPassword("");

        return;
      }

      await login(
        email,
        password
      );

      setLoggedIn(true);

      setMessage(
        "Logged in successfully."
      );

      setPassword("");
    } catch (err) {
      setError(err.message);
    }
  }

  function handleLogout() {
    searchSequence.current += 1;
    setStores([]);
    setSearchedPostalCode("");
    setFailedRetailers([]);
    setIsSearching(false);
    logout();

    setLoggedIn(false);
    setGroceryList([]);
    setSelectedStoreIds([]);
    setComparisonResult(null);

    setMessage(
      "Logged out."
    );

    setError("");
  }

  function handleRetailerChange(event) {
    setSelectedRetailerId(event.target.value);
  }

  async function handleStoreSearch(event) {
    event.preventDefault();
    if (isComparing || isSearching) return;
    const zip = postalCode.trim();
    if (!/^\d{5}$/.test(zip)) {
      setError("Enter a five-digit US ZIP code.");
      return;
    }
    const requestId = ++searchSequence.current;
    setError("");
    setMessage("");
    setStores([]);
    setSearchedPostalCode("");
    setFailedRetailers([]);
    setSelectedStoreIds([]);
    setComparisonResult(null);
    setIsSearching(true);
    try {
      const result = await searchNearbyStores(zip);
      if (requestId !== searchSequence.current) return;
      setStores(result.stores ?? []);
      setFailedRetailers(result.failedRetailers ?? []);
      setSearchedPostalCode(zip);
    } catch (err) {
      if (requestId === searchSequence.current) setError(err.message);
    } finally {
      if (requestId === searchSequence.current) setIsSearching(false);
    }
  }

  function handleQuantityChange(
    itemId,
    value
  ) {
    setQuantities({
      ...quantities,
      [itemId]: value,
    });
  }

  async function handleAddItem(
    itemId
  ) {
    const rawQuantity =
      quantities[itemId] ?? 1;

    const quantity =
      Number(rawQuantity);

    if (
      !Number.isInteger(quantity) ||
      quantity <= 0
    ) {
      setError(
        "Quantity must be a positive whole number."
      );

      return;
    }

    try {
      setError("");
      setMessage("");

      await setGroceryListQuantity(
        itemId,
        quantity
      );

      await refreshGroceryList();

      setMessage(
        "Grocery list updated."
      );
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleCatalogItemCreated(item, created) {
    setItems((current) =>
      current.some((existing) => existing.id === item.id)
        ? current
        : [...current, item].sort((a, b) => a.name.localeCompare(b.name))
    );
    setItemQuery("");

    try {
      setError("");
      await setGroceryListQuantity(item.id, 1);
      await refreshGroceryList();
      setMessage(
        created
          ? `Added ${item.name} to the catalog and your list.`
          : `${item.name} was already in the catalog; added it to your list.`
      );
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleRemoveItem(
    itemId
  ) {
    try {
      setError("");
      setMessage("");

      await removeGroceryListItem(
        itemId
      );

      await refreshGroceryList();

      setComparisonResult(null);

      setMessage(
        "Item removed from grocery list."
      );
    } catch (err) {
      setError(err.message);
    }
  }

  function handleStoreToggle(
    storeId
  ) {
    if (
      selectedStoreIds.includes(
        storeId
      )
    ) {
      setSelectedStoreIds(
        selectedStoreIds.filter(
          (id) => id !== storeId
        )
      );
    } else {
      setSelectedStoreIds([
        ...selectedStoreIds,
        storeId,
      ]);
    }

    setComparisonResult(null);
  }

  async function handleCompare() {
    if (
      selectedStoreIds.length === 0
    ) {
      setError(
        "Select at least one store."
      );

      return;
    }

    if (groceryList.length === 0) {
      setError(
        "Add at least one item to your grocery list first."
      );

      return;
    }

    try {
      setError("");
      setMessage("");
      setIsComparing(true);

      const result =
        await compareStores(
          selectedStoreIds
        );

      setComparisonResult(result);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsComparing(false);
    }
  }

  const visibleStores = selectedRetailerId
    ? stores.filter((store) => String(store.retailerId) === selectedRetailerId)
    : stores;

  // Every word must appear somewhere in the item's name, brand, size or category.
  const itemQueryWords = itemQuery.trim().toLowerCase().split(/\s+/).filter(Boolean);
  const filteredItems = items.filter((item) => {
    const text = [item.name, item.brand, item.size, item.category].join(" ").toLowerCase();
    return itemQueryWords.every((word) => text.includes(word));
  });

  return (
    <div className="app-shell">
      <header className="site-header">
        <div className="header-inner">
          <div className="brand">
            <div
              className="brand-mark"
              aria-hidden="true"
            >
              C
            </div>

            <div>
              <h1>
                CartCompare
              </h1>

              <p className="brand-tagline">
                Compare grocery prices
                across physical stores.
              </p>
            </div>
          </div>

          {loggedIn && (
            <section className="account-area">
              <h2 className="sr-only">
                Account
              </h2>

              <span className="account-status">
                You are logged in.
              </span>

              <button
                className="button button-secondary"
                type="button"
                onClick={handleLogout}
              >
                Logout
              </button>
            </section>
          )}
        </div>
      </header>

      <main className="page-content">
        {error && (
          <div
            className="alert alert-error"
            role="alert"
          >
            {`Error: ${error}`}
          </div>
        )}

        {message && (
          <div
            className="alert alert-success"
            role="status"
          >
            {message}
          </div>
        )}

        {!loggedIn ? (
          <div className="auth-layout">
            <section className="auth-intro">
              <p className="eyebrow">
                Shop smarter
              </p>

              <h2>
                Spend less time checking
                prices store by store.
              </h2>

              <p>
                Build a grocery list,
                choose nearby stores, and
                compare the total cost in
                one place.
              </p>

              <div className="auth-feature-list">
                <div>
                  <span aria-hidden="true">
                    ✓
                  </span>
                  Compare physical store
                  pricing
                </div>

                <div>
                  <span aria-hidden="true">
                    ✓
                  </span>
                  See incomplete pricing
                  clearly
                </div>

                <div>
                  <span aria-hidden="true">
                    ✓
                  </span>
                  Rank stores by your
                  grocery total
                </div>
              </div>
            </section>

            <section className="auth-card">
              <p className="eyebrow">
                Welcome
              </p>

              <h2>
                {authMode === "login"
                  ? "Login"
                  : "Create Account"}
              </h2>

              <form
                className="auth-form"
                onSubmit={
                  handleAuthSubmit
                }
              >
                {authMode ===
                  "register" && (
                  <div className="form-row">
                    <label className="field">
                      <span>
                        First name
                      </span>

                      <input
                        type="text"
                        value={
                          firstName
                        }
                        onChange={(
                          event
                        ) =>
                          setFirstName(
                            event.target
                              .value
                          )
                        }
                        required
                      />
                    </label>

                    <label className="field">
                      <span>
                        Last name
                      </span>

                      <input
                        type="text"
                        value={
                          lastName
                        }
                        onChange={(
                          event
                        ) =>
                          setLastName(
                            event.target
                              .value
                          )
                        }
                        required
                      />
                    </label>
                  </div>
                )}

                <label className="field">
                  <span>Email</span>

                  <input
                    type="email"
                    value={email}
                    onChange={(
                      event
                    ) =>
                      setEmail(
                        event.target
                          .value
                      )
                    }
                    required
                  />
                </label>

                <label className="field">
                  <span>Password</span>

                  <input
                    type="password"
                    value={
                      password
                    }
                    onChange={(
                      event
                    ) =>
                      setPassword(
                        event.target
                          .value
                      )
                    }
                    required
                  />
                </label>

                <button
                  className="button button-primary button-full"
                  type="submit"
                >
                  {authMode ===
                  "login"
                    ? "Login"
                    : "Register"}
                </button>
              </form>

              <button
                className="auth-switch"
                type="button"
                onClick={() => {
                  setAuthMode(
                    authMode ===
                      "login"
                      ? "register"
                      : "login"
                  );

                  setError("");
                  setMessage("");
                }}
              >
                {authMode ===
                "login"
                  ? "Need an account?"
                  : "Already have an account?"}
              </button>
            </section>
          </div>
        ) : (
          <div className="dashboard">
            <section className="dashboard-intro">
              <div>
                <p className="eyebrow">
                  Grocery dashboard
                </p>

                <h2>
                  Build your list and
                  compare stores.
                </h2>

                <p>
                  Add what you need,
                  select the stores you
                  want to check, then let
                  CartCompare rank the
                  results.
                </p>
              </div>

              <div className="dashboard-stat">
                <span>
                  Items on your list
                </span>

                <strong>
                  {groceryList.length}
                </strong>
              </div>
            </section>

            <div className="dashboard-grid">
              <section className="panel">
                <div className="panel-heading">
                  <div>
                    <p className="eyebrow">
                      Step 1
                    </p>

                    <h2>
                      Grocery Items
                    </h2>
                  </div>
                </div>

                {items.length > 0 && (
                  <label className="retailer-field item-search">
                    <span>Search items</span>
                    <input
                      type="search"
                      value={itemQuery}
                      onChange={(event) => setItemQuery(event.target.value)}
                      placeholder="Milk, eggs, coffee..."
                    />
                  </label>
                )}

                {items.length === 0 ? (
                  <p className="empty-state">
                    No items found.
                  </p>
                ) : filteredItems.length === 0 ? (
                  <p className="empty-state">
                    No items match "{itemQuery.trim()}".
                  </p>
                ) : (
                  <div className="item-list">
                    {filteredItems.map(
                      (item) => (
                        <article
                          className="item-card"
                          key={item.id}
                        >
                          <div className="item-info">
                            <div className="item-icon">
                              <span aria-hidden="true">
                                🛒
                              </span>
                            </div>

                            <div>
                              <h3>
                                {
                                  item.name
                                }
                              </h3>

                              <p>
                                {item.brand &&
                                  `${item.brand} · `}

                                {item.size ??
                                  "Size not listed"}
                              </p>
                            </div>
                          </div>

                          <div className="item-actions">
                            <label className="quantity-field">
                              <span>
                                Quantity
                              </span>

                              <input
                                type="number"
                                min="1"
                                value={
                                  quantities[
                                    item.id
                                  ] ?? 1
                                }
                                onChange={(
                                  event
                                ) =>
                                  handleQuantityChange(
                                    item.id,
                                    event
                                      .target
                                      .value
                                  )
                                }
                              />
                            </label>

                            <button
                              className="button button-primary"
                              type="button"
                              onClick={() =>
                                handleAddItem(
                                  item.id
                                )
                              }
                            >
                              Add / Update
                            </button>
                          </div>
                        </article>
                      )
                    )}
                  </div>
                )}

                <CatalogSearch
                  query={itemQuery}
                  stores={stores}
                  onItemCreated={handleCatalogItemCreated}
                />
              </section>

              <section className="panel grocery-list-panel">
                <div className="panel-heading">
                  <div>
                    <p className="eyebrow">
                      Your cart
                    </p>

                    <h2>
                      My Grocery List
                    </h2>
                  </div>

                  <span className="count-badge">
                    {
                      groceryList.length
                    }
                  </span>
                </div>

                {groceryList.length ===
                0 ? (
                  <div className="empty-state">
                    <p>
                      Your grocery list
                      is empty.
                    </p>

                    <span>
                      Add an item to get
                      started.
                    </span>
                  </div>
                ) : (
                  <div className="grocery-list">
                    {groceryList.map(
                      (listItem) => (
                        <div
                          className="grocery-list-row"
                          key={
                            listItem.id
                          }
                        >
                          <div>
                            <strong>
                              {
                                listItem.name
                              }
                            </strong>

                            <p>
                              {listItem.size &&
                                `${listItem.size} · `}

                              Quantity:{" "}
                              {
                                listItem.quantity
                              }
                            </p>
                          </div>

                          <button
                            className="button-link button-danger"
                            type="button"
                            onClick={() =>
                              handleRemoveItem(
                                listItem.itemId
                              )
                            }
                          >
                            Remove
                          </button>
                        </div>
                      )
                    )}
                  </div>
                )}
              </section>
            </div>

            <section className="panel store-panel">
              <div className="panel-heading store-heading">
                <div>
                  <p className="eyebrow">
                    Step 2
                  </p>

                  <h2>
                    Choose Stores
                  </h2>

                  <p className="panel-description">
                    Find nearby locations,
                    then pick stores for
                    your comparison.
                  </p>
                </div>

                <div className="store-search-controls">
                  <form className="store-search-form" onSubmit={handleStoreSearch}>
                    <label className="retailer-field">
                      <span>ZIP code</span>
                      <input
                        type="text"
                        inputMode="numeric"
                        autoComplete="postal-code"
                        maxLength={5}
                        value={postalCode}
                        onChange={(event) => setPostalCode(event.target.value)}
                        placeholder="27613"
                      />
                    </label>
                    <button
                      className="button button-secondary"
                      type="submit"
                      disabled={isSearching || isComparing}
                    >
                      {isSearching ? "Searching..." : "Find Stores"}
                    </button>
                  </form>
                  <label className="retailer-field">
                    <span>Retailer</span>
                    <select value={selectedRetailerId} onChange={handleRetailerChange}>
                      <option value="">All retailers</option>
                      {retailers.map((retailer) => (
                        <option key={retailer.id} value={retailer.id}>
                          {retailer.name}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              </div>

              {isSearching && <p role="status">Finding nearby stores...</p>}
              {failedRetailers.length > 0 && (
                <p className="alert alert-warning" role="status">
                  Could not update stores for: {failedRetailers.map(
                    (retailer) => retailer.name
                  ).join(", ")}.
                </p>
              )}
              {searchedPostalCode && !isSearching && visibleStores.length === 0 && (
                <p className="empty-state">
                  No nearby stores found for ZIP code {searchedPostalCode}
                  {selectedRetailerId ? " at this retailer" : ""}.
                </p>
              )}

              {visibleStores.length > 0 && (
                <div className="store-grid">
                  {visibleStores.map(
                    (store) => {
                      const isSelected =
                        selectedStoreIds.includes(
                          store.id
                        );

                      return (
                        <label
                          className={`store-card ${
                            isSelected
                              ? "store-card-selected"
                              : ""
                          }`}
                          key={
                            store.id
                          }
                        >
                          <input
                            type="checkbox"
                            checked={
                              isSelected
                            }
                            onChange={() =>
                              handleStoreToggle(
                                store.id
                              )
                            }
                          />

                          <div className="store-check">
                            {isSelected
                              ? "✓"
                              : ""}
                          </div>

                          <div className="store-details">
                            <span>{store.retailerName}</span>
                            <strong>
                              {store.name ??
                                "Store"}
                            </strong>

                            <span>
                              {
                                store.addressLine1
                              }
                            </span>

                            <span>
                              {store.city},{" "}
                              {
                                store.state
                              }{" "}
                              {
                                store.postalCode
                              }
                            </span>
                          </div>
                        </label>
                      );
                    }
                  )}
                </div>
              )}

              <div className="compare-bar">
                <p>
                  {`Selected stores: ${selectedStoreIds.length}`}
                </p>

                <button
                  className="button button-primary compare-button"
                  type="button"
                  disabled={
                    isComparing ||
                    selectedStoreIds.length ===
                      0
                  }
                  onClick={
                    handleCompare
                  }
                >
                  {isComparing
                    ? "Refreshing prices..."
                    : "Compare Stores"}
                </button>
              </div>
            </section>

            {comparisonResult && (
              <section className="results-section">
                <div className="results-heading">
                  <div>
                    <p className="eyebrow">
                      Step 3
                    </p>

                    <h2>
                      Comparison Results
                    </h2>
                  </div>

                  <div className="results-summary">
                    <span className="summary-pill summary-complete">
                      {`Complete stores: ${comparisonResult.completeStoreCount}`}
                    </span>

                    <span className="summary-pill">
                      {`Incomplete stores: ${comparisonResult.incompleteStoreCount}`}
                    </span>
                  </div>
                </div>

                {comparisonResult
                  .missingStoreLocationIds
                  ?.length > 0 && (
                  <div className="alert alert-warning">
                    Some selected store
                    IDs no longer exist:{" "}
                    {comparisonResult.missingStoreLocationIds.join(
                      ", "
                    )}
                  </div>
                )}

                <div className="result-grid">
                  {comparisonResult.stores.map(
                    (store) => {
                      const isBest =
                        store.isComplete &&
                        store.rank === 1;

                      return (
                        <article
                          className={`result-card ${
                            isBest
                              ? "result-card-best"
                              : ""
                          } ${
                            !store.isComplete
                              ? "result-card-incomplete"
                              : ""
                          }`}
                          key={
                            store.storeLocationId
                          }
                        >
                          <div className="result-card-header">
                            <div>
                              <div className="result-labels">
                                {isBest && (
                                  <span className="best-price-badge">
                                    Best price
                                  </span>
                                )}

                                {!store.isComplete && (
                                  <span className="incomplete-badge">
                                    Incomplete
                                  </span>
                                )}
                              </div>

                              <h3>
                                {store.rank
                                  ? `#${store.rank} `
                                  : ""}

                                {
                                  store.storeName
                                }
                              </h3>
                            </div>

                            <div className="result-price">
                              <span className="sr-only">
                                {`Known subtotal: $${Number(
                                  store.knownSubtotal
                                ).toFixed(2)}`}
                              </span>

                              <span aria-hidden="true">
                                Known subtotal
                              </span>

                              <strong aria-hidden="true">
                                $
                                {Number(
                                  store.knownSubtotal
                                ).toFixed(
                                  2
                                )}
                              </strong>
                            </div>
                          </div>

                          <div className="result-status-row">
                            <span>
                              {`Status: ${
                                store.isComplete
                                  ? "Complete"
                                  : "Incomplete"
                              }`}
                            </span>

                            {!store.isComplete && (
                              <span>
                                {`Missing items: ${store.missingItemCount}`}
                              </span>
                            )}
                          </div>

                          <div className="result-items">
                            {store.items.map(
                              (
                                resultItem
                              ) => (
                                <div
                                  className="result-item-row"
                                  key={
                                    resultItem.itemId
                                  }
                                >
                                  {resultItem.priceMessage && (
                                    <span className="price-lookup-message" role="status">
                                      {resultItem.priceMessage}
                                    </span>
                                  )}
                                  <span className="sr-only">
                                    {resultItem.isAvailable
                                      ? `${resultItem.itemName} — Qty ${resultItem.quantity} — $${Number(
                                          resultItem.unitPrice
                                        ).toFixed(2)} each — $${Number(
                                          resultItem.lineTotal
                                        ).toFixed(2)} total`
                                      : `${resultItem.itemName} — Qty ${resultItem.quantity} — Price unavailable`}
                                  </span>

                                  <div aria-hidden="true">
                                    <strong>
                                      {
                                        resultItem.itemName
                                      }
                                    </strong>

                                    <span>
                                      Qty{" "}
                                      {
                                        resultItem.quantity
                                      }
                                    </span>
                                  </div>

                                  {resultItem.isAvailable ? (
                                    <div
                                      className="result-item-price"
                                      aria-hidden="true"
                                    >
                                      <span>
                                        $
                                        {Number(
                                          resultItem.unitPrice
                                        ).toFixed(
                                          2
                                        )}{" "}
                                        each
                                      </span>

                                      <strong>
                                        $
                                        {Number(
                                          resultItem.lineTotal
                                        ).toFixed(
                                          2
                                        )}{" "}
                                        total
                                      </strong>
                                    </div>
                                  ) : (
                                    <span
                                      className="unavailable-text"
                                      aria-hidden="true"
                                    >
                                      Price unavailable
                                    </span>
                                  )}
                                </div>
                              )
                            )}
                          </div>
                        </article>
                      );
                    }
                  )}
                </div>
              </section>
            )}
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
