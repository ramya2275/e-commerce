/**
 * CloudShop Microservices Dashboard Application
 * Logic for REST API calls, state management, UI rendering, and cluster health.
 */

// Application State
const state = {
  apiBase: window.location.protocol.startsWith('http') ? window.location.origin : 'http://localhost:5000',
  products: [],
  orders: [],
  cart: new Map(), // productId -> { product, quantity }
  activeTab: 'catalog',
  isCheckingHealth: false
};

// DOM Selectors
const DOM = {
  // Navigation
  tabs: document.querySelectorAll('.nav-tab'),
  views: {
    catalog: document.getElementById('catalog-view'),
    orders: document.getElementById('orders-view'),
    architecture: document.getElementById('architecture-view')
  },
  // Cart
  cartToggleBtn: document.getElementById('cart-toggle-btn'),
  cartCounter: document.getElementById('cart-counter'),
  cartTotalNav: document.getElementById('cart-total-nav'),
  cartDrawer: document.getElementById('cart-drawer'),
  drawerOverlay: document.getElementById('drawer-overlay'),
  drawerCloseBtn: document.getElementById('drawer-close-btn'),
  cartItemsList: document.getElementById('cart-items-list'),
  cartTotalPrice: document.getElementById('cart-total-price'),
  checkoutEmail: document.getElementById('checkout-email'),
  checkoutBtn: document.getElementById('checkout-btn'),
  checkoutAlert: document.getElementById('checkout-alert'),
  // Catalog
  productsGrid: document.getElementById('products-grid'),
  searchInput: document.getElementById('product-search'),
  addProductBtn: document.getElementById('add-product-btn'),
  productCountBadge: document.getElementById('product-count-badge'),
  // Orders
  ordersTableBody: document.getElementById('orders-table-body'),
  ordersEmptyState: document.getElementById('orders-empty-state'),
  // Health
  refreshHealthBtn: document.getElementById('refresh-health-btn'),
  dotGateway: document.getElementById('dot-gateway'),
  dotProduct: document.getElementById('dot-product'),
  dotOrder: document.getElementById('dot-order'),
  metaGateway: document.getElementById('meta-gateway'),
  metaProduct: document.getElementById('meta-product'),
  metaOrder: document.getElementById('meta-order'),
  // Add Product Modal
  addProductModal: document.getElementById('add-product-modal'),
  closeProductModalBtn: document.getElementById('close-product-modal-btn'),
  newProductForm: document.getElementById('new-product-form'),
  // Toasts
  toastContainer: document.getElementById('toast-container')
};

// Icons mapping for products
const productIcons = {
  laptop: '💻',
  keyboard: '⌨️',
  mouse: '🖱️',
  monitor: '🖥️',
  headphones: '🎧',
  default: '📦'
};

function getProductIcon(name) {
  const lower = name.toLowerCase();
  if (lower.includes('laptop') || lower.includes('book')) return productIcons.laptop;
  if (lower.includes('keyboard')) return productIcons.keyboard;
  if (lower.includes('mouse')) return productIcons.mouse;
  if (lower.includes('monitor') || lower.includes('screen')) return productIcons.monitor;
  if (lower.includes('headphone') || lower.includes('audio')) return productIcons.headphones;
  return productIcons.default;
}

// ==========================================================================
// Toast Notifications
// ==========================================================================
function showToast(message, type = 'info') {
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;

  const icon = type === 'success' ? '✓' : type === 'error' ? '✕' : 'ℹ';
  toast.innerHTML = `
    <span style="font-weight: bold;">${icon}</span>
    <span>${message}</span>
  `;

  DOM.toastContainer.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(100%)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// ==========================================================================
// REST API Communication
// ==========================================================================

// Fetch Products from Catalog Service via Gateway
async function fetchProducts(searchTerm = '') {
  try {
    const url = searchTerm 
      ? `${state.apiBase}/api/products?search=${encodeURIComponent(searchTerm)}`
      : `${state.apiBase}/api/products`;

    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP error ${res.status}`);
    const json = await res.json();
    
    if (json.success && json.data) {
      state.products = json.data;
      renderProducts(state.products);
      if (DOM.productCountBadge) {
        DOM.productCountBadge.textContent = `${state.products.length} Products`;
      }
    }
  } catch (err) {
    console.error('Failed to fetch products:', err);
    showToast('Failed to load products. Check Gateway connectivity.', 'error');
    DOM.productsGrid.innerHTML = `
      <div style="grid-column: 1/-1; text-align: center; padding: 40px; color: var(--text-muted);">
        <p>⚠️ Unable to load products from API Gateway (${state.apiBase}).</p>
        <p style="font-size: 0.85rem; margin-top: 8px;">Ensure services are running using <code>.\\run-local.ps1</code> or Docker Compose.</p>
      </div>
    `;
  }
}

// Fetch Orders from Order Service via Gateway
async function fetchOrders() {
  try {
    const res = await fetch(`${state.apiBase}/api/orders`);
    if (!res.ok) throw new Error(`HTTP error ${res.status}`);
    const json = await res.json();

    if (json.success && json.data) {
      state.orders = json.data;
      renderOrders(state.orders);
    }
  } catch (err) {
    console.error('Failed to fetch orders:', err);
    showToast('Failed to load orders.', 'error');
  }
}

// Submit Order to Order Service via Gateway
async function submitOrder(customerEmail) {
  if (state.cart.size === 0) {
    showToast('Your cart is empty.', 'error');
    return;
  }

  const items = Array.from(state.cart.values()).map(item => ({
    productId: item.product.id,
    quantity: item.quantity
  }));

  const payload = {
    customerEmail: customerEmail.trim(),
    items: items
  };

  DOM.checkoutBtn.disabled = true;
  DOM.checkoutBtn.textContent = 'Processing Order...';
  DOM.checkoutAlert.style.display = 'none';

  try {
    const res = await fetch(`${state.apiBase}/api/orders`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const json = await res.json();

    if (res.status === 201 && json.success) {
      showToast('Order confirmed! Stock decremented.', 'success');
      state.cart.clear();
      updateCartUI();
      closeCartDrawer();
      await fetchProducts(DOM.searchInput.value);
      await fetchOrders();
    } else {
      // Show specific error like insufficient stock
      const errorMsg = json.errors?.[0] || json.message || 'Failed to place order.';
      DOM.checkoutAlert.textContent = `❌ ${errorMsg}`;
      DOM.checkoutAlert.style.display = 'block';
      showToast(errorMsg, 'error');
    }
  } catch (err) {
    console.error('Order creation error:', err);
    DOM.checkoutAlert.textContent = `❌ Unable to reach Gateway: ${err.message}`;
    DOM.checkoutAlert.style.display = 'block';
    showToast('Network error during checkout.', 'error');
  } finally {
    DOM.checkoutBtn.disabled = false;
    DOM.checkoutBtn.textContent = 'Confirm & Place Order';
  }
}

// Cancel Order via Order Service (Restores Stock in ProductApi)
async function cancelOrder(orderId) {
  if (!confirm('Are you sure you want to cancel this order? Stock will be automatically restored.')) {
    return;
  }

  try {
    const res = await fetch(`${state.apiBase}/api/orders/${orderId}/cancel`, {
      method: 'POST'
    });

    const json = await res.json();
    if (res.ok && json.success) {
      showToast('Order cancelled and inventory restored!', 'success');
      await fetchOrders();
      await fetchProducts(DOM.searchInput.value);
    } else {
      showToast(json.message || 'Failed to cancel order.', 'error');
    }
  } catch (err) {
    showToast('Error cancelling order: ' + err.message, 'error');
  }
}

// Create a New Product
async function createNewProduct(product) {
  try {
    const res = await fetch(`${state.apiBase}/api/products`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(product)
    });

    const json = await res.json();
    if (res.status === 201 && json.success) {
      showToast(`Product "${product.name}" created!`, 'success');
      closeAddProductModal();
      await fetchProducts();
    } else {
      showToast(json.errors?.[0] || 'Validation error.', 'error');
    }
  } catch (err) {
    showToast('Failed to create product: ' + err.message, 'error');
  }
}

// Adjust Stock Directly (Quick test button)
async function adjustStock(productId, delta) {
  try {
    const res = await fetch(`${state.apiBase}/api/products/${productId}/stock`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ quantityChange: delta })
    });

    const json = await res.json();
    if (res.ok && json.success) {
      showToast(`Stock updated (${delta > 0 ? '+' + delta : delta})`, 'info');
      await fetchProducts(DOM.searchInput.value);
    } else {
      showToast(json.errors?.[0] || json.message, 'error');
    }
  } catch (err) {
    showToast('Failed to adjust stock.', 'error');
  }
}

// Check Health of All Services
async function checkClusterHealth() {
  if (state.isCheckingHealth) return;
  state.isCheckingHealth = true;
  DOM.refreshHealthBtn.textContent = 'Pinging...';

  // 1. Gateway Health
  const t0 = performance.now();
  try {
    const gRes = await fetch(`${state.apiBase}/health/live`);
    const gLatency = Math.round(performance.now() - t0);
    if (gRes.ok) {
      DOM.dotGateway.className = 'pulse-dot';
      DOM.metaGateway.textContent = `200 OK (${gLatency}ms)`;
    } else {
      DOM.dotGateway.className = 'pulse-dot offline';
      DOM.metaGateway.textContent = `HTTP ${gRes.status}`;
    }
  } catch {
    DOM.dotGateway.className = 'pulse-dot offline';
    DOM.metaGateway.textContent = 'Unreachable';
  }

  // 2. Product Service Health (via Gateway routing or direct)
  const t1 = performance.now();
  try {
    const pRes = await fetch(`${state.apiBase}/api/products`);
    const pLatency = Math.round(performance.now() - t1);
    if (pRes.ok) {
      DOM.dotProduct.className = 'pulse-dot';
      DOM.metaProduct.textContent = `Healthy (${pLatency}ms)`;
    } else {
      DOM.dotProduct.className = 'pulse-dot offline';
      DOM.metaProduct.textContent = 'Degraded';
    }
  } catch {
    DOM.dotProduct.className = 'pulse-dot offline';
    DOM.metaProduct.textContent = 'Offline';
  }

  // 3. Order Service Health (via Gateway routing)
  const t2 = performance.now();
  try {
    const oRes = await fetch(`${state.apiBase}/api/orders`);
    const oLatency = Math.round(performance.now() - t2);
    if (oRes.ok) {
      DOM.dotOrder.className = 'pulse-dot';
      DOM.metaOrder.textContent = `Healthy (${oLatency}ms)`;
    } else {
      DOM.dotOrder.className = 'pulse-dot offline';
      DOM.metaOrder.textContent = 'Degraded';
    }
  } catch {
    DOM.dotOrder.className = 'pulse-dot offline';
    DOM.metaOrder.textContent = 'Offline';
  }

  state.isCheckingHealth = false;
  DOM.refreshHealthBtn.textContent = '⟳ Refresh Health';
}

// ==========================================================================
// Rendering Functions
// ==========================================================================

function renderProducts(products) {
  if (products.length === 0) {
    DOM.productsGrid.innerHTML = `
      <div style="grid-column: 1/-1; text-align: center; padding: 40px; color: var(--text-muted);">
        No products match your search.
      </div>
    `;
    return;
  }

  DOM.productsGrid.innerHTML = products.map(product => {
    let stockClass = 'in-stock';
    let stockLabel = `${product.stockQuantity} in stock`;

    if (product.stockQuantity === 0) {
      stockClass = 'out-of-stock';
      stockLabel = 'Sold Out';
    } else if (product.stockQuantity < 10) {
      stockClass = 'low-stock';
      stockLabel = `Only ${product.stockQuantity} left`;
    }

    const icon = getProductIcon(product.name);
    const cartQty = state.cart.get(product.id)?.quantity || 0;
    const canAdd = product.stockQuantity > cartQty;

    return `
      <div class="product-card" data-id="${product.id}">
        <div>
          <div class="product-header">
            <div class="product-icon">${icon}</div>
            <span class="stock-badge ${stockClass}">${stockLabel}</span>
          </div>
          <h3 class="product-title">${escapeHtml(product.name)}</h3>
          <p class="product-description">${escapeHtml(product.description || 'No description provided.')}</p>
        </div>

        <div>
          <div style="display: flex; gap: 8px; margin-bottom: 12px;">
            <button class="health-refresh-btn" onclick="adjustStock('${product.id}', 5)" title="Simulate supplier restock (+5)">
              +5 Stock
            </button>
            <button class="health-refresh-btn" onclick="adjustStock('${product.id}', -5)" title="Simulate inventory reduction (-5)">
              -5 Stock
            </button>
          </div>
          <div class="product-footer">
            <div class="product-price"><span>$</span>${product.price.toFixed(2)}</div>
            <button 
              class="add-to-cart-btn" 
              onclick="addToCart('${product.id}')"
              ${!canAdd ? 'disabled' : ''}
              id="add-btn-${product.id}">
              ${canAdd ? '+ Add to Cart' : 'Max in Cart'}
            </button>
          </div>
        </div>
      </div>
    `;
  }).join('');
}

function renderOrders(orders) {
  if (orders.length === 0) {
    DOM.ordersTableBody.innerHTML = '';
    DOM.ordersEmptyState.style.display = 'block';
    return;
  }

  DOM.ordersEmptyState.style.display = 'none';
  DOM.ordersTableBody.innerHTML = orders.map(order => {
    const isCancelled = order.status === 'Cancelled';
    const statusClass = isCancelled ? 'cancelled' : 'confirmed';
    const dateFormatted = new Date(order.createdAt).toLocaleString();

    const itemsSummary = order.items.map(item => 
      `${escapeHtml(item.productName)} (x${item.quantity})`
    ).join(', ');

    return `
      <tr>
        <td style="font-family: monospace; font-size: 0.8rem; color: var(--accent-cyan-light);">
          ${order.id.substring(0, 8)}...
        </td>
        <td style="font-weight: 500;">${escapeHtml(order.customerEmail)}</td>
        <td style="color: var(--text-secondary); max-width: 320px;" title="${itemsSummary}">
          ${itemsSummary}
        </td>
        <td style="font-weight: 700; color: #ffffff;">$${order.totalAmount.toFixed(2)}</td>
        <td style="color: var(--text-muted); font-size: 0.8rem;">${dateFormatted}</td>
        <td>
          <span class="order-badge ${statusClass}">${order.status}</span>
        </td>
        <td>
          ${!isCancelled ? `
            <button class="btn-cancel-order" onclick="cancelOrder('${order.id}')" title="Cancel order and restore inventory">
              Cancel & Restore
            </button>
          ` : `
            <span style="color: var(--text-muted); font-size: 0.78rem;">Restored</span>
          `}
        </td>
      </tr>
    `;
  }).join('');
}

// ==========================================================================
// Cart Management
// ==========================================================================

function addToCart(productId) {
  const product = state.products.find(p => p.id === productId);
  if (!product) return;

  const currentItem = state.cart.get(productId);
  const currentQty = currentItem ? currentItem.quantity : 0;

  if (currentQty >= product.stockQuantity) {
    showToast(`Cannot add more. Only ${product.stockQuantity} in stock.`, 'error');
    return;
  }

  state.cart.set(productId, {
    product: product,
    quantity: currentQty + 1
  });

  updateCartUI();
  showToast(`Added "${product.name}" to cart.`, 'info');
  openCartDrawer();
}

function updateCartQty(productId, delta) {
  const item = state.cart.get(productId);
  if (!item) return;

  const newQty = item.quantity + delta;
  if (newQty <= 0) {
    state.cart.delete(productId);
  } else {
    if (newQty > item.product.stockQuantity) {
      showToast(`Only ${item.product.stockQuantity} available in stock.`, 'error');
      return;
    }
    item.quantity = newQty;
  }

  updateCartUI();
}

function updateCartUI() {
  let totalCount = 0;
  let totalPrice = 0;

  DOM.cartItemsList.innerHTML = '';

  if (state.cart.size === 0) {
    DOM.cartItemsList.innerHTML = `
      <div style="text-align: center; color: var(--text-muted); padding: 40px 0;">
        <p style="font-size: 2rem; margin-bottom: 8px;">🛒</p>
        <p>Your shopping cart is empty.</p>
      </div>
    `;
  } else {
    state.cart.forEach((item, productId) => {
      totalCount += item.quantity;
      totalPrice += item.quantity * item.product.price;

      const itemCard = document.createElement('div');
      itemCard.className = 'cart-item-card';
      itemCard.innerHTML = `
        <div class="cart-item-info">
          <h4>${escapeHtml(item.product.name)}</h4>
          <p>$${item.product.price.toFixed(2)} × ${item.quantity} = $${(item.quantity * item.product.price).toFixed(2)}</p>
        </div>
        <div class="cart-item-qty">
          <button class="qty-btn" onclick="updateCartQty('${productId}', -1)">−</button>
          <span class="qty-val">${item.quantity}</span>
          <button class="qty-btn" onclick="updateCartQty('${productId}', 1)">+</button>
        </div>
      `;
      DOM.cartItemsList.appendChild(itemCard);
    });
  }

  DOM.cartCounter.textContent = totalCount;
  DOM.cartTotalNav.textContent = `$${totalPrice.toFixed(2)}`;
  DOM.cartTotalPrice.textContent = `$${totalPrice.toFixed(2)}`;
  DOM.checkoutBtn.disabled = totalCount === 0;

  // Re-render product card button disabled states
  renderProducts(state.products);
}

function openCartDrawer() {
  DOM.cartDrawer.classList.add('open');
  DOM.drawerOverlay.classList.add('open');
}

function closeCartDrawer() {
  DOM.cartDrawer.classList.remove('open');
  DOM.drawerOverlay.classList.remove('open');
}

// ==========================================================================
// Add Product Modal
// ==========================================================================

function openAddProductModal() {
  DOM.addProductModal.classList.add('open');
}

function closeAddProductModal() {
  DOM.addProductModal.classList.remove('open');
  DOM.newProductForm.reset();
}

// ==========================================================================
// Helpers & Utilities
// ==========================================================================

function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/[&<>'"]/g, 
    tag => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[tag] || tag)
  );
}

// ==========================================================================
// Event Listeners & Initialization
// ==========================================================================

// Tabs Switching
DOM.tabs.forEach(tab => {
  tab.addEventListener('click', () => {
    DOM.tabs.forEach(t => t.classList.remove('active'));
    tab.classList.add('active');

    const targetTab = tab.dataset.tab;
    Object.keys(DOM.views).forEach(key => {
      DOM.views[key].style.display = key === targetTab ? 'block' : 'none';
    });

    if (targetTab === 'orders') {
      fetchOrders();
    }
  });
});

// Cart Drawer Toggles
DOM.cartToggleBtn.addEventListener('click', openCartDrawer);
DOM.drawerCloseBtn.addEventListener('click', closeCartDrawer);
DOM.drawerOverlay.addEventListener('click', closeCartDrawer);

// Checkout Form Submission
DOM.checkoutBtn.addEventListener('click', () => {
  const email = DOM.checkoutEmail.value;
  if (!email || !email.includes('@')) {
    showToast('Please enter a valid email address.', 'error');
    DOM.checkoutEmail.focus();
    return;
  }
  submitOrder(email);
});

// Search Filter with Debounce
let searchTimeout = null;
DOM.searchInput.addEventListener('input', (e) => {
  clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => {
    fetchProducts(e.target.value.trim());
  }, 250);
});

// Add Product Modal
DOM.addProductBtn.addEventListener('click', openAddProductModal);
DOM.closeProductModalBtn.addEventListener('click', closeAddProductModal);
DOM.addProductModal.addEventListener('click', (e) => {
  if (e.target === DOM.addProductModal) closeAddProductModal();
});

DOM.newProductForm.addEventListener('submit', (e) => {
  e.preventDefault();
  const name = document.getElementById('new-product-name').value.trim();
  const description = document.getElementById('new-product-desc').value.trim();
  const price = parseFloat(document.getElementById('new-product-price').value);
  const stockQuantity = parseInt(document.getElementById('new-product-stock').value, 10);

  if (!name || isNaN(price) || isNaN(stockQuantity)) {
    showToast('Please fill out all required fields.', 'error');
    return;
  }

  createNewProduct({ name, description, price, stockQuantity });
});

// Health Refresh
DOM.refreshHealthBtn.addEventListener('click', checkClusterHealth);

// Make functions globally accessible for inline onclick handlers
window.addToCart = addToCart;
window.updateCartQty = updateCartQty;
window.cancelOrder = cancelOrder;
window.adjustStock = adjustStock;

// Initial Load
document.addEventListener('DOMContentLoaded', () => {
  fetchProducts();
  fetchOrders();
  checkClusterHealth();
  // Periodic health check every 15 seconds
  setInterval(checkClusterHealth, 15000);
});
