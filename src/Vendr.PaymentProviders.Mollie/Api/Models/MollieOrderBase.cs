using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public abstract class MollieOrderBase<TOrderLine>
        where TOrderLine : MollieOrderLineBase
    {
        [JsonProperty("amount")]
        public MollieAmount Amount { get; set; }

        [JsonProperty("orderNumber")]
        public string OrderNumber { get; set; }

        [JsonProperty("orderNumber")]
        public IEnumerable<TOrderLine> Lines { get; set; }

        [JsonProperty("billingAddress")]
        public MollieAddress BillingAddress { get; set; }

        [JsonProperty("shippingAddress")]
        public MollieAddress ShippingAddress { get; set; }

        [JsonProperty("consumerDateOfBirth")]
        public DateTime ConsumerDateOfBirth { get; set; }

        [JsonProperty("redirectUrl")]
        public string RedirectUrl { get; set; }

        [JsonProperty("webhookUrl")]
        public string WebhookUrl { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("method")]
        public IEnumerable<string> Method { get; set; }

        [JsonProperty("payment")]
        public MolliePayment Payment { get; set; }

        [JsonProperty("metadata")]
        public object Metadata { get; set; }

        [JsonProperty("expiresAt")]
        public virtual DateTime ExpiresAt { get; set; }

        [JsonProperty("shopperCountryMustMatchBillingCountry")]
        public bool ShopperCountryMustMatchBillingCountry { get; set; }
    }
}
