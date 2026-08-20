import type { CookAtHomeOrder, CookAtHomeOrderStatus, Food, FoodCousin, Order, OrderStatus, User } from './types'

const base = (import.meta.env.VITE_API_BASE_URL || 'https://localhost:7001').replace(/\/$/, '')

async function request<T>(path:string, init:RequestInit = {}, retry = true):Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) headers.set('Content-Type','application/json')
  let response = await fetch(`${base}${path}`, { ...init, headers, credentials:'include' })
  if (response.status === 401 && retry && path !== '/api/v1/auth/refresh' && path !== '/api/v1/auth/login') {
    const refresh = await fetch(`${base}/api/v1/auth/refresh`, { method:'POST', credentials:'include' })
    if (refresh.ok) response = await fetch(`${base}${path}`, { ...init, headers, credentials:'include' })
  }
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`
    try { const p = await response.json(); message = p.detail || p.title || message } catch { /* ignore */ }
    throw new Error(message)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  me: () => request<User>('/api/v1/auth/me'),
  register: (payload:unknown) => request<User>('/api/v1/auth/register',{method:'POST',body:JSON.stringify(payload)},false),
  login: (email:string,password:string) => request<User>('/api/v1/auth/login',{method:'POST',body:JSON.stringify({email,password})},false),
  logout: () => request<void>('/api/v1/auth/logout',{method:'POST'},false),
  foods: () => request<Food[]>('/api/v1/foods',{},false),
  cousins: (id:string) => request<FoodCousin[]>(`/api/v1/foods/${id}/cousins`,{},false),
  myFoods: () => request<Food[]>('/api/v1/foods/mine'),
  createFood: (payload:unknown) => request<Food>('/api/v1/foods',{method:'POST',body:JSON.stringify(payload)}),
  updateFood: (id:string,payload:unknown) => request<Food>(`/api/v1/foods/${id}`,{method:'PUT',body:JSON.stringify(payload)}),
  uploadFoodImage: (id:string,file:File) => { const data=new FormData(); data.append('file',file); return request<Food>(`/api/v1/foods/${id}/image`,{method:'POST',body:data}) },
  placeOrder: (items:{foodId:string;quantity:number}[],fulfilmentType:string,deliveryAddress:string|undefined,idempotencyKey:string) => request<Order>('/api/v1/orders',{method:'POST',headers:{'Idempotency-Key':idempotencyKey},body:JSON.stringify({items,fulfilmentType,deliveryAddress})}),
  myOrders: () => request<Order[]>('/api/v1/orders/mine'),
  cookOrders: () => request<Order[]>('/api/v1/orders/cook'),
  updateOrderStatus: (id:string,status:OrderStatus) => request<Order>(`/api/v1/orders/${id}/status`,{method:'PUT',body:JSON.stringify({status})}),

  requestCookAtHome: (payload:unknown,idempotencyKey:string) => request<CookAtHomeOrder>('/api/v1/cook-at-home-orders',{method:'POST',headers:{'Idempotency-Key':idempotencyKey},body:JSON.stringify(payload)}),
  myCookAtHomeOrders: () => request<CookAtHomeOrder[]>('/api/v1/cook-at-home-orders/mine'),
  cookAtHomeOrdersForCook: () => request<CookAtHomeOrder[]>('/api/v1/cook-at-home-orders/cook'),
  confirmCookAtHomeQuote: (id:string) => request<CookAtHomeOrder>(`/api/v1/cook-at-home-orders/${id}/confirm-quote`,{method:'POST'}),
  cancelCookAtHomeOrder: (id:string) => request<CookAtHomeOrder>(`/api/v1/cook-at-home-orders/${id}/cancel`,{method:'POST'}),
  updateCookAtHomeStatus: (id:string,status:CookAtHomeOrderStatus) => request<CookAtHomeOrder>(`/api/v1/cook-at-home-orders/${id}/status`,{method:'PUT',body:JSON.stringify({status})})
}
