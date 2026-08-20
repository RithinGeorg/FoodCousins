import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { api } from './api'
import type {
  CartItem,
  CookAtHomeOrder,
  CookAtHomeOrderStatus,
  Food,
  FoodCousin,
  Order,
  OrderStatus,
  User
} from './types'

type View = 'menu' | 'cook-at-home' | 'orders' | 'cook' | 'auth'

export default function App() {
  const [user, setUser] = useState<User | null>(null)
  const [foods, setFoods] = useState<Food[]>([])
  const [cart, setCart] = useState<CartItem[]>([])
  const [view, setView] = useState<View>('menu')
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    Promise.all([api.foods(), api.me().catch(() => null)])
      .then(([foodList, currentUser]) => {
        setFoods(foodList)
        setUser(currentUser)
      })
      .finally(() => setLoading(false))
  }, [])

  const cartTotal = useMemo(() => cart.reduce((sum, item) => sum + item.food.price * item.quantity, 0), [cart])

  const addToCart = (food: Food) => {
    setMessage('')
    setCart(current => {
      if (current.length && current[0].food.cookProfileId !== food.cookProfileId) {
        setMessage('One food order can contain dishes from one cook only. Clear the cart to order from another cook.')
        return current
      }

      const found = current.find(item => item.food.id === food.id)
      return found
        ? current.map(item => item.food.id === food.id ? { ...item, quantity: item.quantity + 1 } : item)
        : [...current, { food, quantity: 1 }]
    })
  }

  const logout = async () => {
    await api.logout()
    setUser(null)
    setCart([])
    setView('menu')
  }

  if (loading) return <div className="loading">Loading FoodCousins…</div>

  return (
    <div className="app">
      <header>
        <button className="brand" onClick={() => setView('menu')}>
          FoodCousins<span>Every food has a cousin.</span>
        </button>
        <nav>
          <button onClick={() => setView('menu')}>Menu</button>
          <button onClick={() => setView('cook-at-home')}>Cook at home</button>
          {user?.role === 'Customer' && <button onClick={() => setView('orders')}>My orders</button>}
          {user?.role === 'Cook' && <button onClick={() => setView('cook')}>Cook dashboard</button>}
          {user ? (
            <>
              <span className="user">Hi, {user.displayName}</span>
              <button onClick={logout}>Logout</button>
            </>
          ) : (
            <button className="primary" onClick={() => setView('auth')}>Login / Register</button>
          )}
        </nav>
      </header>

      {message && <div className="notice">{message}<button onClick={() => setMessage('')}>×</button></div>}

      <main>
        {view === 'menu' && (
          <Menu
            foods={foods}
            add={addToCart}
            cart={cart}
            setCart={setCart}
            cartTotal={cartTotal}
            user={user}
            onAuth={() => setView('auth')}
            onOrdered={() => setView('orders')}
          />
        )}
        {view === 'cook-at-home' && (
          <CookAtHomeRequest
            foods={foods}
            user={user}
            onAuth={() => setView('auth')}
            onRequested={() => setView('orders')}
          />
        )}
        {view === 'auth' && <Auth onUser={u => { setUser(u); setView(u.role === 'Cook' ? 'cook' : 'menu') }} />}
        {view === 'orders' && user?.role === 'Customer' && <CustomerOrders />}
        {view === 'cook' && user?.role === 'Cook' && <CookDashboard refreshPublic={async () => setFoods(await api.foods())} />}
      </main>

      <footer>FoodCousins Dev MVP · Food discovery · Home-cooked ordering · Cook-at-home requests</footer>
    </div>
  )
}

function Menu({ foods, add, cart, setCart, cartTotal, user, onAuth, onOrdered }: {
  foods: Food[]
  add: (food: Food) => void
  cart: CartItem[]
  setCart: (items: CartItem[]) => void
  cartTotal: number
  user: User | null
  onAuth: () => void
  onOrdered: () => void
}) {
  const [fulfilment, setFulfilment] = useState('Pickup')
  const [address, setAddress] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [cousins, setCousins] = useState<FoodCousin[]>([])
  const [cousinOf, setCousinOf] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())

  useEffect(() => setIdempotencyKey(crypto.randomUUID()), [cart])

  const visible = foods.filter(food => !search.trim() || `${food.name} ${food.cuisine} ${food.tags || ''} ${food.description || ''}`.toLowerCase().includes(search.trim().toLowerCase()))

  const findCousins = async (food: Food) => {
    setError('')
    try {
      setCousinOf(food.name)
      setCousins(await api.cousins(food.id))
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const place = async () => {
    if (!user) { onAuth(); return }
    if (user.role !== 'Customer') { setError('Use a Customer account to place orders.'); return }
    setBusy(true)
    setError('')
    try {
      await api.placeOrder(cart.map(item => ({ foodId: item.food.id, quantity: item.quantity })), fulfilment, address || undefined, idempotencyKey)
      setCart([])
      setIdempotencyKey(crypto.randomUUID())
      onOrdered()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="layout">
      <section>
        <div className="hero">
          <p className="eyebrow">HOME-COOKED · LOCAL · DISCOVERABLE</p>
          <h1>Find something you love.<br />Meet its cousin.</h1>
          <p>Browse dishes prepared by local cooks, order food, or book a cook to prepare a selected dish at your home.</p>
        </div>
        <div className="searchbar"><input placeholder="Search dishes, cuisines or tags…" value={search} onChange={e => setSearch(e.target.value)} /></div>
        {cousinOf && (
          <div className="cousinbox">
            <div className="cousintitle"><h2>Cousins of {cousinOf}</h2><button onClick={() => { setCousinOf(''); setCousins([]) }}>×</button></div>
            {cousins.length ? (
              <div className="cousins">{cousins.map(c => <button key={c.food.id} onClick={() => add(c.food)}><b>{c.food.name}</b><span>{c.food.cuisine} · {Math.round(c.score * 100)}% match</span><small>{c.why}</small></button>)}</div>
            ) : <p className="muted">Add more menu items to discover cousins.</p>}
          </div>
        )}
        <div className="cards">
          {visible.length ? visible.map(food => (
            <article className="food" key={food.id}>
              {food.imageUrl ? <img src={food.imageUrl} alt={food.name} /> : <div className="placeholder">🍲</div>}
              <div className="foodbody">
                <div className="meta"><span>{food.cuisine}</span><span>{food.cookName}</span></div>
                <h3>{food.name}</h3>
                <p>{food.description || 'Home-cooked and made to order.'}</p>
                <div className="foodbottom">
                  <strong>${food.price.toFixed(2)}</strong>
                  <div className="foodactions"><button className="small" onClick={() => findCousins(food)}>Cousins</button><button className="primary" onClick={() => add(food)}>Add</button></div>
                </div>
              </div>
            </article>
          )) : <div className="empty">No dishes yet. Register as a cook and add the first one.</div>}
        </div>
      </section>
      <aside className="cart">
        <h2>Your food order</h2>
        {!cart.length ? <p className="muted">Your cart is empty.</p> : <>
          {cart.map(item => (
            <div className="cartline" key={item.food.id}>
              <div><b>{item.food.name}</b><small>{item.food.cookName}</small></div>
              <div className="qty">
                <button onClick={() => setCart(cart.map(i => i.food.id === item.food.id ? { ...i, quantity: Math.max(1, i.quantity - 1) } : i))}>−</button>
                <span>{item.quantity}</span>
                <button onClick={() => setCart(cart.map(i => i.food.id === item.food.id ? { ...i, quantity: i.quantity + 1 } : i))}>+</button>
              </div>
            </div>
          ))}
          <div className="total"><span>Total</span><b>${cartTotal.toFixed(2)}</b></div>
          <label>Fulfilment<select value={fulfilment} onChange={e => setFulfilment(e.target.value)}><option>Pickup</option><option>Delivery</option></select></label>
          {fulfilment === 'Delivery' && <label>Delivery address<textarea value={address} onChange={e => setAddress(e.target.value)} required /></label>}
          {error && <p className="error">{error}</p>}
          <button className="primary wide" disabled={busy} onClick={place}>{busy ? 'Placing…' : user ? 'Place order' : 'Login to order'}</button>
          <button className="link" onClick={() => setCart([])}>Clear cart</button>
        </>}
      </aside>
    </div>
  )
}

function CookAtHomeRequest({ foods, user, onAuth, onRequested }: { foods: Food[]; user: User | null; onAuth: () => void; onRequested: () => void }) {
  const [foodId, setFoodId] = useState(foods[0]?.id || '')
  const [peopleCount, setPeopleCount] = useState(2)
  const [location, setLocation] = useState('')
  const [requestedFor, setRequestedFor] = useState('')
  const [contactName, setContactName] = useState(user?.displayName || '')
  const [contactPhone, setContactPhone] = useState('')
  const [instructions, setInstructions] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())

  useEffect(() => {
    if (!foodId && foods[0]) setFoodId(foods[0].id)
  }, [foodId, foods])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!user) { onAuth(); return }
    if (user.role !== 'Customer') { setError('Use a Customer account to request a cook at home.'); return }
    if (!requestedFor) { setError('Choose the requested date and time.'); return }

    setBusy(true)
    setError('')
    try {
      await api.requestCookAtHome({
        foodId,
        peopleCount,
        location,
        requestedForUtc: new Date(requestedFor).toISOString(),
        contactName,
        contactPhone,
        specialInstructions: instructions || null
      }, idempotencyKey)
      setIdempotencyKey(crypto.randomUUID())
      onRequested()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="auth">
      <div className="panel">
        <p className="eyebrow">COOK AT HOME</p>
        <h1>Request a cook for your home</h1>
        <p className="muted">Choose a dish, number of people, location and time. The request is priced asynchronously and you can confirm the quote when it is ready.</p>
        <form onSubmit={submit}>
          <label>Dish<select value={foodId} onChange={e => setFoodId(e.target.value)} required><option value="" disabled>Select a dish</option>{foods.map(food => <option key={food.id} value={food.id}>{food.name} · {food.cookName}</option>)}</select></label>
          <label>People<input type="number" min="1" max="100" value={peopleCount} onChange={e => setPeopleCount(Number(e.target.value))} required /></label>
          <label>Location<textarea value={location} onChange={e => setLocation(e.target.value)} placeholder="Full address or service location" required /></label>
          <label>Date and time<input type="datetime-local" value={requestedFor} onChange={e => setRequestedFor(e.target.value)} required /></label>
          <label>Contact name<input value={contactName} onChange={e => setContactName(e.target.value)} required /></label>
          <label>Contact phone<input value={contactPhone} onChange={e => setContactPhone(e.target.value)} required /></label>
          <label>Special instructions<textarea value={instructions} onChange={e => setInstructions(e.target.value)} placeholder="Dietary needs, kitchen/access notes, preferred menu details…" /></label>
          {error && <p className="error">{error}</p>}
          <button className="primary wide" disabled={busy || !foods.length}>{busy ? 'Requesting…' : user ? 'Request quote' : 'Login to request'}</button>
        </form>
      </div>
    </section>
  )
}

function Auth({ onUser }: { onUser: (user: User) => void }) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [name, setName] = useState('')
  const [type, setType] = useState('Customer')
  const [business, setBusiness] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      const currentUser = mode === 'login'
        ? await api.login(email, password)
        : await api.register({ email, password, displayName: name, accountType: type, businessName: type === 'Cook' ? business : null })
      onUser(currentUser)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return <section className="auth"><div className="panel"><div className="tabs"><button className={mode === 'login' ? 'active' : ''} onClick={() => setMode('login')}>Login</button><button className={mode === 'register' ? 'active' : ''} onClick={() => setMode('register')}>Register</button></div><form onSubmit={submit}>{mode === 'register' && <><label>Name<input value={name} onChange={e => setName(e.target.value)} required /></label><label>Account<select value={type} onChange={e => setType(e.target.value)}><option>Customer</option><option>Cook</option></select></label>{type === 'Cook' && <label>Business / kitchen name<input value={business} onChange={e => setBusiness(e.target.value)} required /></label>}</>}<label>Email<input type="email" value={email} onChange={e => setEmail(e.target.value)} required /></label><label>Password<input type="password" minLength={10} value={password} onChange={e => setPassword(e.target.value)} required /></label>{error && <p className="error">{error}</p>}<button className="primary wide" disabled={busy}>{busy ? 'Please wait…' : mode === 'login' ? 'Login' : 'Create account'}</button></form></div></section>
}

function CustomerOrders() {
  const [orders, setOrders] = useState<Order[]>([])
  const [homeOrders, setHomeOrders] = useState<CookAtHomeOrder[]>([])
  const [error, setError] = useState('')

  const load = async () => {
    const [foodOrders, cookAtHomeOrders] = await Promise.all([api.myOrders(), api.myCookAtHomeOrders()])
    setOrders(foodOrders)
    setHomeOrders(cookAtHomeOrders)
  }

  useEffect(() => { load().catch(e => setError(e.message)) }, [])

  const confirmQuote = async (order: CookAtHomeOrder) => {
    try { await api.confirmCookAtHomeQuote(order.id); await load() } catch (e) { setError((e as Error).message) }
  }

  const cancel = async (order: CookAtHomeOrder) => {
    try { await api.cancelCookAtHomeOrder(order.id); await load() } catch (e) { setError((e as Error).message) }
  }

  return (
    <section>
      <h1>My orders</h1>
      {error && <p className="error">{error}</p>}
      <h2>Cook-at-home requests</h2>
      <CookAtHomeList orders={homeOrders} actions={order => <>
        {order.status === 'PriceCalculated' && <button className="small" onClick={() => confirmQuote(order)}>Confirm quote</button>}
        {['Requested', 'Processing', 'PriceCalculated', 'Confirmed', 'AcceptedByCook'].includes(order.status) && <button className="small" onClick={() => cancel(order)}>Cancel</button>}
      </>} />
      <h2 className="sectiontitle">Food orders</h2>
      <OrderList orders={orders} />
    </section>
  )
}

const nextStates: Partial<Record<OrderStatus, OrderStatus[]>> = {
  Pending: ['Confirmed', 'Cancelled'],
  Confirmed: ['AcceptedByCook', 'Rejected'],
  AcceptedByCook: ['Preparing', 'Cancelled'],
  Preparing: ['Ready'],
  Ready: ['Completed']
}

const nextCookAtHomeStates: Partial<Record<CookAtHomeOrderStatus, CookAtHomeOrderStatus[]>> = {
  Confirmed: ['AcceptedByCook', 'Rejected'],
  AcceptedByCook: ['Preparing'],
  Preparing: ['Completed']
}

function CookDashboard({ refreshPublic }: { refreshPublic: () => Promise<void> }) {
  const [foods, setFoods] = useState<Food[]>([])
  const [orders, setOrders] = useState<Order[]>([])
  const [homeOrders, setHomeOrders] = useState<CookAtHomeOrder[]>([])
  const [name, setName] = useState('')
  const [cuisine, setCuisine] = useState('')
  const [price, setPrice] = useState('')
  const [description, setDescription] = useState('')
  const [tags, setTags] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    const [myFoods, foodOrders, cookAtHomeOrders] = await Promise.all([api.myFoods(), api.cookOrders(), api.cookAtHomeOrdersForCook()])
    setFoods(myFoods)
    setOrders(foodOrders)
    setHomeOrders(cookAtHomeOrders)
  }

  useEffect(() => { load().catch(e => setError(e.message)) }, [])

  const addFood = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await api.createFood({ name, cuisine, price: Number(price), description, tags, isAvailable: true })
      setName(''); setCuisine(''); setPrice(''); setDescription(''); setTags('')
      await load(); await refreshPublic()
    } catch (err) { setError((err as Error).message) }
  }

  const image = async (food: Food, file?: File) => {
    if (!file) return
    try { await api.uploadFoodImage(food.id, file); await load(); await refreshPublic() } catch (err) { setError((err as Error).message) }
  }

  const toggleAvailability = async (food: Food) => {
    try {
      await api.updateFood(food.id, { name: food.name, description: food.description, cuisine: food.cuisine, price: food.price, tags: food.tags, isAvailable: !food.isAvailable })
      await load(); await refreshPublic()
    } catch (err) { setError((err as Error).message) }
  }

  const status = async (order: Order, next: OrderStatus) => {
    try { await api.updateOrderStatus(order.id, next); await load() } catch (err) { setError((err as Error).message) }
  }

  const homeStatus = async (order: CookAtHomeOrder, next: CookAtHomeOrderStatus) => {
    try { await api.updateCookAtHomeStatus(order.id, next); await load() } catch (err) { setError((err as Error).message) }
  }

  return (
    <section>
      <h1>Cook dashboard</h1>
      {error && <p className="error">{error}</p>}
      <div className="cookgrid">
        <div className="panel">
          <h2>Add a dish</h2>
          <form onSubmit={addFood}>
            <label>Name<input value={name} onChange={e => setName(e.target.value)} required /></label>
            <label>Cuisine<input value={cuisine} onChange={e => setCuisine(e.target.value)} required /></label>
            <label>Price / base per-person cook-at-home price<input type="number" min="0.01" step="0.01" value={price} onChange={e => setPrice(e.target.value)} required /></label>
            <label>Description<textarea value={description} onChange={e => setDescription(e.target.value)} /></label>
            <label>Tags (comma separated)<input value={tags} onChange={e => setTags(e.target.value)} placeholder="rice, spicy, chicken" /></label>
            <button className="primary">Add dish</button>
          </form>
        </div>
        <div>
          <h2>Your menu</h2>
          {foods.map(food => <div className="manage" key={food.id}><div><b>{food.name}</b><small>{food.cuisine} · ${food.price.toFixed(2)} · {food.isAvailable ? 'Available' : 'Paused'}</small></div><div className="actions"><button className="small" onClick={() => toggleAvailability(food)}>{food.isAvailable ? 'Pause' : 'Activate'}</button><label className="upload">Upload photo<input type="file" accept="image/*" onChange={e => image(food, e.target.files?.[0])} /></label></div></div>)}
        </div>
      </div>

      <h2 className="sectiontitle">Cook-at-home requests</h2>
      <CookAtHomeList orders={homeOrders} actions={order => <>{(nextCookAtHomeStates[order.status] || []).map(next => <button key={next} className="small" onClick={() => homeStatus(order, next)}>{next}</button>)}</>} />

      <h2 className="sectiontitle">Food orders</h2>
      <OrderList orders={orders} actions={order => <>{(nextStates[order.status] || []).map(next => <button key={next} className="small" onClick={() => status(order, next)}>{next}</button>)}</>} />
    </section>
  )
}

function CookAtHomeList({ orders, actions }: { orders: CookAtHomeOrder[]; actions?: (order: CookAtHomeOrder) => ReactNode }) {
  return <div className="orders">{orders.length ? orders.map(order => <article className="order" key={order.id}><div className="orderhead"><div><b>{order.orderNumber}</b><small>{new Date(order.createdAtUtc).toLocaleString()}</small></div><span className={`status status-${order.status}`}>{order.status}</span></div><p><b>{order.foodName}</b> for {order.peopleCount} people · {new Date(order.requestedForUtc).toLocaleString()}</p><p>{order.location}</p>{order.specialInstructions && <p className="muted">{order.specialInstructions}</p>}<div className="orderfoot"><span>{order.cookName} · {order.contactName} · {order.contactPhone}</span><b>{order.estimatedPrice == null ? 'Quote pending' : `$${order.estimatedPrice.toFixed(2)}`}</b></div>{actions && <div className="actions">{actions(order)}</div>}</article>) : <div className="empty">No cook-at-home requests yet.</div>}</div>
}

function OrderList({ orders, actions }: { orders: Order[]; actions?: (order: Order) => ReactNode }) {
  return <div className="orders">{orders.length ? orders.map(order => <article className="order" key={order.id}><div className="orderhead"><div><b>{order.orderNumber}</b><small>{new Date(order.createdAtUtc).toLocaleString()}</small></div><span className={`status status-${order.status}`}>{order.status}</span></div><p>{order.items.map(item => `${item.quantity}× ${item.foodName}`).join(', ')}</p><div className="orderfoot"><span>{order.fulfilmentType}{order.deliveryAddress ? ` · ${order.deliveryAddress}` : ''}</span><b>${order.totalAmount.toFixed(2)}</b></div>{actions && <div className="actions">{actions(order)}</div>}</article>) : <div className="empty">No food orders yet.</div>}</div>
}
