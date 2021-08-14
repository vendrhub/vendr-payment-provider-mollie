using Mollie.Api.Client;
using Mollie.Api.Models.Order;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using System.Globalization;

using MollieAmmount = Mollie.Api.Models.Amount;
using MollieLocale = Mollie.Api.Models.Payment.Locale;
using MolliePaymentStatus = Mollie.Api.Models.Payment.PaymentStatus;
using MollieOrderStatus = Mollie.Api.Models.Order.OrderStatus;
using MollieOrderLineStatus = Mollie.Api.Models.Order.OrderLineStatus;
using MollieOrderLineType = Mollie.Api.Models.Order.OrderLineDetailsType;

namespace Vendr.PaymentProviders.Mollie
{
    [PaymentProvider("mollie-onetime", "Mollie (One Time)", "Mollie payment provider for one time payments")]
    public class MollieOneTimePaymentProvider : PaymentProviderBase<MollieOneTimeSettings>
    {
        private readonly IPaymentProviderUriResolver _uriResolver;

        public MollieOneTimePaymentProvider(VendrContext vendr,
            IPaymentProviderUriResolver uriResolver)
            : base(vendr)
        {
            _uriResolver = uriResolver;
        }

        public override bool CanFetchPaymentStatus => true;
        public override bool CanCancelPayments => false;
        public override bool CanRefundPayments => false;
        public override bool CanCapturePayments => false;
        public override bool FinalizeAtContinueUrl => false;

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, MollieOneTimeSettings settings)
        {
            // Validate settings
            settings.MustNotBeNull("settings");

            if (settings.TestMode)
            {
                settings.TestApiKey.MustNotBeNull("settings.TestApiKey");
            }
            else
            {
                settings.LiveApiKey.MustNotBeNull("settings.LiveApiKey");
            }

            // Get entities
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
            var country = Vendr.Services.CountryService.GetCountry(order.PaymentInfo.CountryId.Value);
            var paymentMethod = Vendr.Services.PaymentMethodService.GetPaymentMethod(order.PaymentInfo.PaymentMethodId.Value);
            var shippingMethod = order.ShippingInfo.ShippingMethodId.HasValue
                ? Vendr.Services.ShippingMethodService.GetShippingMethod(order.ShippingInfo.ShippingMethodId.Value)
                : null;

            // Adjustments helper
            var processPriceAdjustments = new Action<IReadOnlyCollection<PriceAdjustment>, List<OrderLineRequest>, string>((adjustments, orderLines, namePrefix) =>
            {
                foreach (var adjustment in adjustments)
                {
                    var isDiscount = adjustment.Price.WithTax < 0;
                    var taxRate = (adjustment.Price.WithTax / adjustment.Price.WithoutTax) - 1;

                    orderLines.Add(new OrderLineRequest
                    {
                        Sku = isDiscount ? "DISCOUNT" : "SURCHARGE",
                        Name = (namePrefix + " " + (isDiscount ? "Discount" : "Fee") + " - " + adjustment.Name).Trim(),
                        Type = isDiscount ? MollieOrderLineType.Discount : MollieOrderLineType.Surcharge,
                        Quantity = 1,
                        UnitPrice = new MollieAmmount(currency.Code, adjustment.Price.WithTax),
                        VatRate = (taxRate * 100).ToString("0.00", CultureInfo.InvariantCulture),
                        VatAmount = new MollieAmmount(currency.Code, adjustment.Price.Tax),
                        TotalAmount = new MollieAmmount(currency.Code, adjustment.Price.WithTax)
                    });
                }
            });

            // Create the order
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);

            var mollieOrderAddress = new OrderAddressDetails
            {
                GivenName = order.CustomerInfo.FirstName,
                FamilyName = order.CustomerInfo.LastName,
                Email = order.CustomerInfo.Email,
                Country = country.Code
            };

            if (!string.IsNullOrWhiteSpace(settings.BillingAddressLine1PropertyAlias))
                mollieOrderAddress.StreetAndNumber = order.Properties[settings.BillingAddressLine1PropertyAlias];
            if (!string.IsNullOrWhiteSpace(settings.BillingAddressCityPropertyAlias))
                mollieOrderAddress.City = order.Properties[settings.BillingAddressCityPropertyAlias];
            if (!string.IsNullOrWhiteSpace(settings.BillingAddressStatePropertyAlias))
                mollieOrderAddress.Region = order.Properties[settings.BillingAddressStatePropertyAlias];
            if (!string.IsNullOrWhiteSpace(settings.BillingAddressZipCodePropertyAlias))
                mollieOrderAddress.PostalCode = order.Properties[settings.BillingAddressZipCodePropertyAlias];

            var mollieOrderLines = new List<OrderLineRequest>();

            // Process order lines
            foreach (var orderLine in order.OrderLines)
            {
                var mollieOrderLine = new OrderLineRequest
                {
                    Sku = orderLine.Sku,
                    Name = orderLine.Name,
                    Quantity = (int)orderLine.Quantity,
                    UnitPrice = new MollieAmmount(currency.Code, orderLine.UnitPrice.WithoutAdjustments.WithTax),
                    VatRate = (orderLine.TaxRate.Value * 100).ToString("0.00", CultureInfo.InvariantCulture),
                    VatAmount = new MollieAmmount(currency.Code, orderLine.TotalPrice.Value.Tax),
                    TotalAmount = new MollieAmmount(currency.Code, orderLine.TotalPrice.Value.WithTax)
                };

                if (orderLine.TotalPrice.TotalAdjustment.WithTax < 0)
                {
                    mollieOrderLine.DiscountAmount = new MollieAmmount(currency.Code, orderLine.TotalPrice.TotalAdjustment.WithTax * -1);
                }
                else if (orderLine.TotalPrice.TotalAdjustment.WithTax > 0)
                {
                    // Not sure we can handle an order line fee?
                }

                if (!string.IsNullOrWhiteSpace(settings.OrderLineProductTypePropertyAlias))
                {
                    mollieOrderLine.Type = orderLine.Properties[settings.OrderLineProductTypePropertyAlias];
                }

                if (!string.IsNullOrWhiteSpace(settings.OrderLineProductCategoryPropertyAlias))
                {
                    mollieOrderLine.Category = orderLine.Properties[settings.OrderLineProductCategoryPropertyAlias];
                }

                mollieOrderLines.Add(mollieOrderLine);
            }

            // Process subtotal price adjustments
            if (order.SubtotalPrice.Adjustments.Count > 0)
                processPriceAdjustments.Invoke(order.SubtotalPrice.Adjustments, mollieOrderLines, "Subtotal");

            // Process payment fee
            if (order.PaymentInfo.TotalPrice.WithoutAdjustments.WithTax > 0)
            {
                var paymentOrderLine = new OrderLineRequest
                {
                    Sku = !string.IsNullOrWhiteSpace(paymentMethod.Sku) ? paymentMethod.Sku : "PF001",
                    Name = paymentMethod.Name + " Fee",
                    Type = MollieOrderLineType.Surcharge,
                    Quantity = 1,
                    UnitPrice = new MollieAmmount(currency.Code, order.PaymentInfo.TotalPrice.WithoutAdjustments.WithTax),
                    VatRate = (order.PaymentInfo.TaxRate.Value * 100).ToString("0.00", CultureInfo.InvariantCulture),
                    VatAmount = new MollieAmmount(currency.Code, order.PaymentInfo.TotalPrice.Value.Tax),
                    TotalAmount = new MollieAmmount(currency.Code, order.PaymentInfo.TotalPrice.Value.WithTax)
                };

                if (order.PaymentInfo.TotalPrice.Adjustment.WithTax < 0)
                {
                    paymentOrderLine.DiscountAmount = new MollieAmmount(currency.Code, order.PaymentInfo.TotalPrice.Adjustment.WithTax * -1);
                }
                else if (order.PaymentInfo.TotalPrice.Adjustment.WithTax > 0)
                {
                    // Not sure we can handle an order line fee?
                }

                mollieOrderLines.Add(paymentOrderLine);
            }

            // Process shipping fee
            if (shippingMethod != null && order.ShippingInfo.TotalPrice.WithoutAdjustments.WithTax > 0)
            {
                var shippingOrderLine = new OrderLineRequest
                {
                    Sku = !string.IsNullOrWhiteSpace(shippingMethod.Sku) ? shippingMethod.Sku : "SF001",
                    Name = shippingMethod.Name + " Fee",
                    Type = MollieOrderLineType.ShippingFee,
                    Quantity = 1,
                    UnitPrice = new MollieAmmount(currency.Code, order.ShippingInfo.TotalPrice.WithoutAdjustments.WithTax),
                    VatRate = (order.ShippingInfo.TaxRate.Value * 100).ToString("0.00", CultureInfo.InvariantCulture),
                    VatAmount = new MollieAmmount(currency.Code, order.ShippingInfo.TotalPrice.Value.Tax),
                    TotalAmount = new MollieAmmount(currency.Code, order.ShippingInfo.TotalPrice.Value.WithTax)
                };

                if (order.ShippingInfo.TotalPrice.Adjustment.WithTax < 0)
                {
                    shippingOrderLine.DiscountAmount = new MollieAmmount(currency.Code, order.ShippingInfo.TotalPrice.Adjustment.WithTax * -1);
                }
                else if (order.ShippingInfo.TotalPrice.Adjustment.WithTax > 0)
                {
                    // Not sure we can handle an order line fee?
                }

                mollieOrderLines.Add(shippingOrderLine);
            }

            // Process total price adjustments
            if (order.TotalPrice.Adjustments.Count > 0)
                processPriceAdjustments.Invoke(order.TotalPrice.Adjustments, mollieOrderLines, "Total");

            // Process gift cards
            var giftCards = order.TransactionAmount.Adjustments.OfType<GiftCardAdjustment>().ToList();
            if (giftCards.Count > 0)
            {
                foreach (var giftCard in giftCards)
                {
                    mollieOrderLines.Add(new OrderLineRequest
                    {
                        Sku = "GIFT_CARD",
                        Name = "Gift Card - " + giftCard.GiftCardCode,
                        Type = MollieOrderLineType.GiftCard,
                        Quantity = 1,
                        UnitPrice = new MollieAmmount(currency.Code, giftCard.Amount.Value),
                        VatRate = "0.00",
                        VatAmount = new MollieAmmount(currency.Code, 0m),
                        TotalAmount = new MollieAmmount(currency.Code, giftCard.Amount.Value)
                    });
                }
            }

            // Process other adjustment types
            var amountAdjustments = order.TransactionAmount.Adjustments.Where(x => !(x is GiftCardAdjustment)).ToList();
            if (amountAdjustments.Count > 0)
            {
                foreach (var adjustment in amountAdjustments)
                {
                    var isDiscount = adjustment.Amount.Value < 0;

                    mollieOrderLines.Add(new OrderLineRequest
                    {
                        Sku = isDiscount ? "DISCOUNT" : "SURCHARGE",
                        Name = "Transaction " + (isDiscount ? "Discount" : "Fee") + " - " + adjustment.Name,
                        Type = isDiscount ? MollieOrderLineType.Discount : MollieOrderLineType.Surcharge,
                        Quantity = 1,
                        UnitPrice = new MollieAmmount(currency.Code, adjustment.Amount.Value),
                        VatRate = "0.00",
                        VatAmount = new MollieAmmount(currency.Code, 0m),
                        TotalAmount = new MollieAmmount(currency.Code, adjustment.Amount.Value)
                    });
                }
            }

            var mollieOrderRequest = new OrderRequest
            {
                Amount = new MollieAmmount(currency.Code.ToUpperInvariant(), order.TransactionAmount.Value),
                OrderNumber = order.OrderNumber,
                Lines = mollieOrderLines,
                Metadata = order.GenerateOrderReference(),
                BillingAddress = mollieOrderAddress,
                RedirectUrl = callbackUrl + "?redirect=true", // Explicitly redirect to the callback URL as this will need to do more processing to decide where to redirect to
                WebhookUrl = callbackUrl,
                Locale = !string.IsNullOrWhiteSpace(settings.Locale) ? settings.Locale : MollieLocale.en_US,
            };

            if (!string.IsNullOrWhiteSpace(settings.PaymentMethods))
            {
                var paymentMethods = settings.PaymentMethods.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (paymentMethods.Count == 1)
                {
                    mollieOrderRequest.Method = paymentMethods[0];
                }
                else if (paymentMethods.Count > 1)
                {
                    mollieOrderRequest.Methods = paymentMethods;
                }
            }

            var mollieOrderResult = mollieOrderClient.CreateOrderAsync(mollieOrderRequest).GetAwaiter().GetResult();

            return new PaymentFormResult
            {
                Form = new PaymentForm(mollieOrderResult.Links.Checkout.Href, FormMethod.Get),
                MetaData = new Dictionary<string, string>()
                {
                    { "mollieOrderId", mollieOrderResult.Id }
                }
            };
        }

        public override string GetCancelUrl(OrderReadOnly order, MollieOneTimeSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.CancelUrl.MustNotBeNull("settings.CancelUrl");

            return settings.CancelUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, MollieOneTimeSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ErrorUrl.MustNotBeNull("settings.ErrorUrl");

            return settings.ErrorUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, MollieOneTimeSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, MollieOneTimeSettings settings)
        {
            if (request.QueryString.AllKeys.Contains("redirect"))
            {
                return ProcessRedirectCallback(order, request, settings);
            }
            else
            {
                return ProcessWebhookCallback(order, request, settings);
            }
        }

        private CallbackResult ProcessRedirectCallback(OrderReadOnly order, HttpRequestBase request, MollieOneTimeSettings settings)
        {
            var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Found);

            var mollieOrderId = order.Properties["mollieOrderId"];
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true).GetAwaiter().GetResult();

            if (mollieOrder.Embedded.Payments.All(x => x.Status == MolliePaymentStatus.Canceled))
            {
                response.Headers.Location = new Uri(_uriResolver.GetCancelUrl(Alias, order.GenerateOrderReference(), Vendr.Security.HashProvider));
            }
            else
            {
                response.Headers.Location = new Uri(_uriResolver.GetContinueUrl(Alias, order.GenerateOrderReference(), Vendr.Security.HashProvider));
            }

            return new CallbackResult
            {
                HttpResponse = response
            };
        }

        private CallbackResult ProcessWebhookCallback(OrderReadOnly order, HttpRequestBase request, MollieOneTimeSettings settings)
        {
            // Validate the ID from the webhook matches the orders mollieOrderId property
            var id = request.Form["id"];
            var mollieOrderId = order.Properties["mollieOrderId"];
            if (id != mollieOrderId)
                return CallbackResult.Ok();

            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true, true).GetAwaiter().GetResult();

            return CallbackResult.Ok(new TransactionInfo
            {
                AmountAuthorized = decimal.Parse(mollieOrder.Amount.Value, CultureInfo.InvariantCulture),
                TransactionFee = 0m,
                TransactionId = mollieOrderId,
                PaymentStatus = GetPaymentStatus(mollieOrder)
            });
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, MollieOneTimeSettings settings)
        {
            var mollieOrderId = order.Properties["mollieOrderId"];
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true, true).GetAwaiter().GetResult();

            return new ApiResult
            {
                TransactionInfo = new TransactionInfoUpdate()
                {
                    TransactionId = order.TransactionInfo.TransactionId,
                    PaymentStatus = GetPaymentStatus(mollieOrder)
                }
            };
        }

        private PaymentStatus GetPaymentStatus(OrderResponse order)
        {
            // The order is refunded if the total refunded amount is
            // greater than or equal to the original amount of the order
            if (order.AmountRefunded != null)
            {
                var amount = decimal.Parse(order.Amount.Value, CultureInfo.InvariantCulture);
                var amountRefunded = decimal.Parse(order.AmountRefunded.Value, CultureInfo.InvariantCulture);

                if (amountRefunded >= amount)
                {
                    return PaymentStatus.Refunded;
                }
            }

            // If the order is in a shipping status, at least one of the order lines
            // should be in an authorized or paid status. If there are any authorized
            // rows, then set the whole order as authorized, otherwise we'll see it's 
            // captured.
            if (order.Status == MollieOrderStatus.Shipping)
            {
                if (order.Lines.Any(x => x.Status == MollieOrderLineStatus.Authorized))
                {
                    return PaymentStatus.Authorized;
                }
                else
                {
                    return PaymentStatus.Captured;
                }
            }

            // If the order is completed, there is at least one order line that is completed and
            // paid for. If all order lines were canceled, then the whole order would be cancelled
            if (order.Status == MollieOrderStatus.Paid || order.Status == MollieOrderStatus.Completed)
                return PaymentStatus.Captured;

            if (order.Status == MollieOrderStatus.Canceled || order.Status == MollieOrderStatus.Expired)
                return PaymentStatus.Cancelled;

            if (order.Status == MollieOrderStatus.Authorized)
                return PaymentStatus.Authorized;

            return PaymentStatus.PendingExternalSystem;
        }
    }
}
