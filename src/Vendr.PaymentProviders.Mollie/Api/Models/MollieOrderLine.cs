using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieOrderLine : MollieOrderLineBase, IMollieHalResource
    {
        public static class Statuses
        {
            public const string Created = "created";
            public const string Authorized = "authorized";
            public const string Paid = "paid";
            public const string Canceled = "canceled";
            public const string Shipping = "shipping";
            public const string Completed = "completed";
        }

        public static class Types
        {
            public const string Physical = "physical";
            public const string Discount = "discount";
            public const string Digital = "digital";
            public const string ShippingFee = "shipping_fee";
            public const string StoreCredit = "store_credit";
            public const string GiftCard = "gift_card";
            public const string Surcharge = "surcharge";
        }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("orderId")]
        public string OrderId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("isCancelable")]
        public bool IsCancelable { get; set; }

        [JsonProperty("quantityShipped")]
        public int QuantityShipped { get; set; }

        [JsonProperty("amountShipped")]
        public MollieAmount AmountShipped { get; set; }

        [JsonProperty("quantityRefunded")]
        public int QuantityRefunded { get; set; }

        [JsonProperty("amountRefunded")]
        public MollieAmount AmountRefunded { get; set; }

        [JsonProperty("quantityCanceled")]
        public int QuantityCanceled { get; set; }

        [JsonProperty("amountCanceled")]
        public MollieAmount AmountCanceled { get; set; }

        [JsonProperty("shippableQuantity")]
        public int ShippableQuantity { get; set; }

        [JsonProperty("refundableQuantity")]
        public int RefundableQuantity { get; set; }

        [JsonProperty("cancelableQuantity")]
        public int CancelableQuantity { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("_links")]
        public IDictionary<string, MollieHalLink> Links { get; set; }
    }
}
