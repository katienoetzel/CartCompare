import {
  useEffect,
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
  getStoresByRetailer,
} from "./api/stores";

import {
  compareStores,
} from "./api/comparisons";

import "./App.css";

function App() {
  // -----------------------------------------
  // General application data
  // -----------------------------------------

  const [retailers, setRetailers] =
    useState([]);

  const [items, setItems] =
    useState([]);

  const [groceryList, setGroceryList] =
    useState([]);

  const [stores, setStores] =
    useState([]);

  const [
    comparisonResult,
    setComparisonResult,
  ] = useState(null);

  // -----------------------------------------
  // Authentication state
  // -----------------------------------------

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

  // -----------------------------------------
  // Grocery-list state
  // -----------------------------------------

  const [quantities, setQuantities] =
    useState({});

  // -----------------------------------------
  // Store-selection state
  // -----------------------------------------

  const [
    selectedRetailerId,
    setSelectedRetailerId,
  ] = useState("");

  const [
    selectedStoreIds,
    setSelectedStoreIds,
  ] = useState([]);

  // -----------------------------------------
  // UI feedback
  // -----------------------------------------

  const [message, setMessage] =
    useState("");

  const [error, setError] =
    useState("");

  const [isComparing, setIsComparing] =
    useState(false);

  // -----------------------------------------
  // Load public application data
  // -----------------------------------------

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

  // -----------------------------------------
  // Load user's grocery list after login
  // -----------------------------------------

  useEffect(() => {
    if (!loggedIn) {
      setGroceryList([]);
      return;
    }

    refreshGroceryList();
  }, [loggedIn]);

  // -----------------------------------------
  // Load stores when retailer changes
  // -----------------------------------------

  useEffect(() => {
    if (!selectedRetailerId) {
      setStores([]);
      setSelectedStoreIds([]);
      return;
    }

    async function loadStores() {
      try {
        setError("");

        const data =
          await getStoresByRetailer(
            selectedRetailerId
          );

        setStores(data);

        setSelectedStoreIds([]);
        setComparisonResult(null);
      } catch (err) {
        setError(err.message);
      }
    }

    loadStores();
  }, [selectedRetailerId]);

  // -----------------------------------------
  // Grocery-list refresh helper
  // -----------------------------------------

  async function refreshGroceryList() {
    try {
      const data =
        await getGroceryList();

      setGroceryList(data);
    } catch (err) {
      setError(err.message);
    }
  }

  // -----------------------------------------
  // Authentication
  // -----------------------------------------

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

  // -----------------------------------------
  // Grocery list
  // -----------------------------------------

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

  // -----------------------------------------
  // Store selection
  // -----------------------------------------

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

  // -----------------------------------------
  // Compare stores
  // -----------------------------------------

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

  // -----------------------------------------
  // Render
  // -----------------------------------------

  return (
    <main>
      <h1>CartCompare</h1>

      <p>
        Compare grocery prices
        across physical stores.
      </p>

      {error && (
        <p>
          Error: {error}
        </p>
      )}

      {message && (
        <p>{message}</p>
      )}

      {!loggedIn ? (
        <section>
          <h2>
            {authMode === "login"
              ? "Login"
              : "Create Account"}
          </h2>

          <form
            onSubmit={
              handleAuthSubmit
            }
          >
            {authMode ===
              "register" && (
              <>
                <div>
                  <label>
                    First name
                    <input
                      type="text"
                      value={
                        firstName
                      }
                      onChange={(
                        event
                      ) =>
                        setFirstName(
                          event
                            .target
                            .value
                        )
                      }
                      required
                    />
                  </label>
                </div>

                <div>
                  <label>
                    Last name
                    <input
                      type="text"
                      value={
                        lastName
                      }
                      onChange={(
                        event
                      ) =>
                        setLastName(
                          event
                            .target
                            .value
                        )
                      }
                      required
                    />
                  </label>
                </div>
              </>
            )}

            <div>
              <label>
                Email
                <input
                  type="email"
                  value={email}
                  onChange={(
                    event
                  ) =>
                    setEmail(
                      event
                        .target
                        .value
                    )
                  }
                  required
                />
              </label>
            </div>

            <div>
              <label>
                Password
                <input
                  type="password"
                  value={
                    password
                  }
                  onChange={(
                    event
                  ) =>
                    setPassword(
                      event
                        .target
                        .value
                    )
                  }
                  required
                />
              </label>
            </div>

            <button
              type="submit"
            >
              {authMode ===
              "login"
                ? "Login"
                : "Register"}
            </button>
          </form>

          <button
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
      ) : (
        <>
          <section>
            <h2>Account</h2>

            <p>
              You are logged in.
            </p>

            <button
              type="button"
              onClick={
                handleLogout
              }
            >
              Logout
            </button>
          </section>

          <section>
            <h2>
              Grocery Items
            </h2>

            {items.length ===
            0 ? (
              <p>
                No items found.
              </p>
            ) : (
              <ul>
                {items.map(
                  (item) => (
                    <li
                      key={
                        item.id
                      }
                    >
                      <strong>
                        {
                          item.name
                        }
                      </strong>

                      {item.brand &&
                        ` — ${item.brand}`}

                      {item.size &&
                        ` — ${item.size}`}

                      <div>
                        <label>
                          Quantity{" "}
                          <input
                            type="number"
                            min="1"
                            value={
                              quantities[
                                item
                                  .id
                              ] ??
                              1
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
                    </li>
                  )
                )}
              </ul>
            )}
          </section>

          <section>
            <h2>
              My Grocery List
            </h2>

            {groceryList.length ===
            0 ? (
              <p>
                Your grocery list
                is empty.
              </p>
            ) : (
              <ul>
                {groceryList.map(
                  (listItem) => (
                    <li
                      key={
                        listItem.id
                      }
                    >
                      <strong>
                        {
                          listItem.name
                        }
                      </strong>

                      {listItem.size &&
                        ` — ${listItem.size}`}

                      {" — "}
                      Quantity:{" "}
                      {
                        listItem.quantity
                      }

                      <button
                        type="button"
                        onClick={() =>
                          handleRemoveItem(
                            listItem.itemId
                          )
                        }
                      >
                        Remove
                      </button>
                    </li>
                  )
                )}
              </ul>
            )}
          </section>

          <section>
            <h2>
              Choose Stores
            </h2>

            <label>
              Retailer{" "}
              <select
                value={
                  selectedRetailerId
                }
                onChange={(
                  event
                ) =>
                  setSelectedRetailerId(
                    event
                      .target
                      .value
                  )
                }
              >
                <option value="">
                  Select a
                  retailer
                </option>

                {retailers.map(
                  (retailer) => (
                    <option
                      key={
                        retailer.id
                      }
                      value={
                        retailer.id
                      }
                    >
                      {
                        retailer.name
                      }
                    </option>
                  )
                )}
              </select>
            </label>

            {selectedRetailerId &&
              stores.length ===
                0 && (
                <p>
                  No stores found
                  for this
                  retailer.
                </p>
              )}

            {stores.length >
              0 && (
              <div>
                {stores.map(
                  (store) => (
                    <div
                      key={
                        store.id
                      }
                    >
                      <label>
                        <input
                          type="checkbox"
                          checked={selectedStoreIds.includes(
                            store.id
                          )}
                          onChange={() =>
                            handleStoreToggle(
                              store.id
                            )
                          }
                        />

                        {" "}

                        {store.name ??
                          "Store"}

                        {" — "}

                        {
                          store.addressLine1
                        }

                        {", "}

                        {
                          store.city
                        }

                        {", "}

                        {
                          store.state
                        }

                        {" "}

                        {
                          store.postalCode
                        }
                      </label>
                    </div>
                  )
                )}
              </div>
            )}

            <p>
              Selected stores:{" "}
              {
                selectedStoreIds.length
              }
            </p>

            <button
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
                ? "Comparing..."
                : "Compare Stores"}
            </button>
          </section>

          {comparisonResult && (
            <section>
              <h2>
                Comparison Results
              </h2>

              <p>
                Complete stores:{" "}
                {
                  comparisonResult.completeStoreCount
                }
              </p>

              <p>
                Incomplete stores:{" "}
                {
                  comparisonResult.incompleteStoreCount
                }
              </p>

              {comparisonResult
                .missingStoreLocationIds
                ?.length >
                0 && (
                <p>
                  Some selected
                  store IDs no
                  longer exist:{" "}
                  {comparisonResult.missingStoreLocationIds.join(
                    ", "
                  )}
                </p>
              )}

              {comparisonResult.stores.map(
                (store) => (
                  <article
                    key={
                      store.storeLocationId
                    }
                  >
                    <h3>
                      {store.rank
                        ? `#${store.rank} `
                        : ""}

                      {
                        store.storeName
                      }
                    </h3>

                    <p>
                      Known subtotal:{" "}
                      $
                      {Number(
                        store.knownSubtotal
                      ).toFixed(
                        2
                      )}
                    </p>

                    <p>
                      Status:{" "}
                      {store.isComplete
                        ? "Complete"
                        : "Incomplete"}
                    </p>

                    {!store.isComplete && (
                      <p>
                        Missing items:{" "}
                        {
                          store.missingItemCount
                        }
                      </p>
                    )}

                    <ul>
                      {store.items.map(
                        (
                          resultItem
                        ) => (
                          <li
                            key={
                              resultItem.itemId
                            }
                          >
                            <strong>
                              {
                                resultItem.itemName
                              }
                            </strong>

                            {" — "}

                            Qty{" "}
                            {
                              resultItem.quantity
                            }

                            {resultItem.isAvailable ? (
                              <>
                                {" — $"}

                                {Number(
                                  resultItem.unitPrice
                                ).toFixed(
                                  2
                                )}

                                {" each"}

                                {" — $"}

                                {Number(
                                  resultItem.lineTotal
                                ).toFixed(
                                  2
                                )}

                                {" total"}
                              </>
                            ) : (
                              <>
                                {
                                  " — Price unavailable"
                                }
                              </>
                            )}
                          </li>
                        )
                      )}
                    </ul>
                  </article>
                )
              )}
            </section>
          )}
        </>
      )}
    </main>
  );
}

export default App;