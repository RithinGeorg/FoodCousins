export type User = { id:string; email:string; displayName:string; role:'Customer'|'Cook'|'Admin'; businessName?:string|null }
export type Food = { id:string; cookProfileId:string; cookName:string; name:string; description?:string|null; cuisine:string; tags?:string|null; price:number; isAvailable:boolean; imageUrl?:string|null }
export type OrderStatus = 'Pending'|'AwaitingPayment'|'Confirmed'|'AcceptedByCook'|'Preparing'|'Ready'|'Completed'|'Cancelled'|'Rejected'|'PaymentFailed'|'Refunded'
export type Order = { id:string; orderNumber:string; customerId:string; cookProfileId:string; cookName:string; status:OrderStatus; totalAmount:number; fulfilmentType:string; deliveryAddress?:string|null; createdAtUtc:string; items:{foodId:string;foodName:string;unitPrice:number;quantity:number}[] }
export type CartItem = { food: Food; quantity:number }
export type FoodCousin = { food: Food; score:number; why:string }

export type CookAtHomeOrderStatus = 'Requested'|'Processing'|'PriceCalculated'|'Confirmed'|'AcceptedByCook'|'Preparing'|'Completed'|'Cancelled'|'Rejected'
export type CookAtHomeOrder = {
  id:string
  orderNumber:string
  customerId:string
  cookProfileId:string
  cookName:string
  foodId:string
  foodName:string
  peopleCount:number
  location:string
  requestedForUtc:string
  contactName:string
  contactPhone:string
  specialInstructions?:string|null
  status:CookAtHomeOrderStatus
  estimatedPrice?:number|null
  createdAtUtc:string
  updatedAtUtc:string
}
